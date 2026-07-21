using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DevDude.AWSUploader.Editor
{
    public class AWSUploaderWindow : EditorWindow
    {
        private AWSUploadSettings _settings;
        private Vector2 _logScroll;
        private CancellationTokenSource _cancellationSource;
        private bool _isUploading;
        private string _log = "";
        private readonly ConcurrentQueue<UploadProgressData> _progressQueue = new();
        private float _currentProgress;
        private string _currentProgressText;
        private UploadPlan _currentPlan;
        private string _planText;
        private string _localFolder;
        private string _detectedBuildPath;
        private string _detectedVersion;
        private BuildTarget _targetPlatform;

        private const string _SETTINGS_KEY = "AWSUploader.Settings";

        public class UploadProgressData
        {
            public int Completed;
            public int Total;
            public float Progress;
        }

        private void OnEnable()
        {
            _targetPlatform = EditorUserBuildSettings.activeBuildTarget;

            var settingsPath = EditorPrefs.GetString(_SETTINGS_KEY, "");

            if (!string.IsNullOrEmpty(settingsPath))
            {
                _settings = AssetDatabase.LoadAssetAtPath<AWSUploadSettings>(settingsPath);
            }
        }

        private void SaveState()
        {
            if (_settings != null)
            {
                EditorPrefs.SetString(_SETTINGS_KEY, AssetDatabase.GetAssetPath(_settings));
            }
        }

        private void Update()
        {
            while (_progressQueue.TryDequeue(out var data))
            {
                _currentProgress = data.Progress;
                _currentProgressText = $"Uploading {data.Completed}/{data.Total} files";
                EditorUtility.DisplayProgressBar("AWS Upload", _currentProgressText, _currentProgress);
            }
        }

        [MenuItem("Tools/DevDude/Addressables Uploader")]
        public static void Open()
        {
            GetWindow<AWSUploaderWindow>($"AWS Upload (v{AWSUploader.VERSION.ToString()})");
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_detectedBuildPath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Detected:\n{_detectedVersion}\n\n{_detectedBuildPath}", MessageType.Info);
            }

            EditorGUILayout.Space();
            _settings = (AWSUploadSettings)EditorGUILayout.ObjectField("Settings", _settings, typeof(AWSUploadSettings), false);
            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_planText))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Upload Plan", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_planText, GUILayout.Height(250));
            }

            using (new EditorGUI.DisabledScope(_isUploading))
            {
                if (GUILayout.Button("Detect Latest Addressables Build"))
                {
                    DetectLatestBuild();
                }
            }

            using (new EditorGUI.DisabledScope(_isUploading || _settings == null))
            {
                if (GUILayout.Button("Generate Upload Plan"))
                {
                    _ = GeneratePlan();
                }
            }

            using (new EditorGUI.DisabledScope(_isUploading || _settings == null))
            {
                if (GUILayout.Button("Upload"))
                {
                    StartUpload();
                }
            }

            using (new EditorGUI.DisabledScope(!_isUploading))
            {
                if (GUILayout.Button("Cancel"))
                {
                    CancelUpload();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll);

            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                SaveState();
            }
        }

        private async void StartUpload()
        {
            if (!ValidateBeforeUpload(out var error))
            {
                _log = "Validation Failed:\n" + error;
                return;
            }

            _isUploading = true;

            _cancellationSource = new CancellationTokenSource();

            try
            {
                if (string.IsNullOrEmpty(_localFolder))
                {
                    _log = "Addressables output folder is empty.";
                    _isUploading = false;
                    return;
                }

                var config = CreateConfig();
                using var uploader = new AWSUploader(config);

                uploader.OnUploadProgress += (completed, total, progress) =>
                {
                    _progressQueue.Enqueue(new UploadProgressData
                    {
                        Completed = completed,
                        Total = total,
                        Progress = progress
                    });
                };

                await uploader.UploadFolderAsync(_cancellationSource.Token);
                _log += "\nUpload Completed";
            }
            catch (System.OperationCanceledException)
            {
                _log += "\nUpload Cancelled";
            }
            catch (System.Exception ex)
            {
                _log +=
                    $"\nError:\n{ex}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isUploading = false;

                _cancellationSource?.Dispose();
                _cancellationSource = null;

                Repaint();
            }
        }

        private bool ValidateBeforeUpload(out string error)
        {
            error = null;

            if (_settings == null)
            {
                error = "Upload settings not assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.serviceUrl))
            {
                error = "S3 Service URL is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.bucketName))
            {
                error = "Bucket name is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.accessKey))
            {
                error = "S3 credentials are empty.";
                return false;
            }

            if (string.IsNullOrEmpty(_localFolder))
            {
                error = "Addressables folder not detected.";
                return false;
            }

            if (!Directory.Exists(_localFolder))
            {
                error = $"Addressables folder not found:\n{_localFolder}";
                return false;
            }

            var catalogExists = Directory.GetFiles(_localFolder, "catalog*.json", SearchOption.AllDirectories).Any();

            if (!catalogExists)
            {
                error = "Addressables catalog json not found.";
                return false;
            }

            var bundleCount = Directory.GetFiles(_localFolder, "*.bundle", SearchOption.AllDirectories).Length;

            if (bundleCount == 0)
            {
                error = "No Addressables bundles found.";
                return false;
            }

            if (_currentPlan == null)
            {
                error = "Upload plan not generated.";
                return false;
            }

            if (_currentPlan.Upload.Count == 0)
            {
                error = "Nothing to upload.";
                return false;
            }

            foreach (var file in _currentPlan.Upload)
            {
                var fullPath = Path.Combine(_localFolder, file);

                if (!File.Exists(fullPath))
                {
                    error = $"Upload file missing:\n{fullPath}";
                    return false;
                }
            }

            return true;
        }

        private UploadConfig CreateConfig()
        {
            return new UploadConfig
            {
                ServiceUrl = _settings.serviceUrl,
                BucketName = _settings.bucketName,
                AccessKey = _settings.accessKey,
                RemoteFolder = _settings.remoteFolder,
                ManifestFileName = _settings.manifestFileName,
                LocalFolder = _localFolder,
                ParallelUploads = _settings.parallelUploads,
                RetryCount = _settings.retryCount,
                DeleteRemovedFiles = _settings.deleteRemovedFiles,
                RemoteRoot =
                    $"{_settings.remoteFolder}/" +
                    $"{_detectedVersion}/" +
                    $"{GetPlatformFolder()}"
            };
        }

        private void CancelUpload()
        {
            _cancellationSource?.Cancel();
        }

        private async Task GeneratePlan()
        {
            if (string.IsNullOrEmpty(_localFolder))
            {
                _log = "Detect Addressables build first.";
                return;
            }

            try
            {
                _log = "Generating plan...\n";

                var config = CreateConfig();

                using var uploader = new AWSUploader(config);

                _log += "Creating local manifest...\n";

                var localManifest = UploadManifest.Create(config);

                _log += $"Local files: {localManifest.Files.Count}\n";

                var remoteManifest = await uploader.GetRemoteManifestAsync();

                _log += $"Remote files: {remoteManifest.Files.Count}\n";

                _currentPlan = uploader.CreateUploadPlan(localManifest, remoteManifest);

                _planText = _currentPlan.GetDetailedSummary();

                _log += "Plan generated.";
            }
            catch (Exception e)
            {
                _log += "\nERROR:\n" + e;
            }

            Repaint();
        }

        private void DetectLatestBuild()
        {
            _targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            _currentPlan = null;
            _planText = null;

            try
            {
                var platformFolder = GetPlatformFolder();
                var root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ServerData", _settings.localFolder);

                if (!Directory.Exists(root))
                {
                    _log = $"ServerData not found:\n{root}";
                    return;
                }

                var versions = Directory.GetDirectories(root);

                if (versions.Length == 0)
                {
                    _log = "No Addressables versions found.";
                    return;
                }

                var latestVersionFolder = versions.OrderByDescending(path =>
                    {
                        var name = Path.GetFileName(path);
                        return Version.TryParse(name, out var version) ? version : new Version(0, 0);
                    }).First();

                _detectedVersion = Path.GetFileName(latestVersionFolder);
                _detectedBuildPath = Path.Combine(latestVersionFolder, platformFolder);

                if (!Directory.Exists(_detectedBuildPath))
                {
                    _log = $"Platform build not found:\n{_detectedBuildPath}";
                    return;
                }

                _localFolder = _detectedBuildPath;

                _log =
                    $"Latest Build Found\n\n" +
                    $"Game: {_settings.localFolder}\n" +
                    $"Platform: {platformFolder}\n" +
                    $"Version: {_detectedVersion}\n" +
                    $"Path: {_detectedBuildPath}";
            }
            catch (Exception e)
            {
                _log = e.ToString();
            }

            Repaint();
        }

        private string GetPlatformFolder()
        {
            return _targetPlatform switch
            {
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                BuildTarget.StandaloneWindows64 => "StandaloneWindows64",

                _ => _targetPlatform.ToString()
            };
        }
    }
}