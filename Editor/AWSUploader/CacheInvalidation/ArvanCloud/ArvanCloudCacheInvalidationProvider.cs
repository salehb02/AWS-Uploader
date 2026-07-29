using Newtonsoft.Json;
using DevDude.AWSUploader.CacheInvalidation;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevDude.AWSUploader.CacheInvalidation.ArvanCloud
{
    /// <summary>
    /// ArvanCloud CDN implementation of the generic cache invalidation contract.
    /// </summary>
    public sealed class ArvanCloudCacheInvalidationProvider : ICacheInvalidationProvider
    {
        private readonly HttpClient _client;
        private readonly string _apiBaseUrl;
        private readonly string _domain;

        public string ProviderName => "ArvanCloud CDN";

        public ArvanCloudCacheInvalidationProvider(string apiBaseUrl, string domain, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                throw new ArgumentException("Arvan CDN API URL is empty.", nameof(apiBaseUrl));

            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Arvan CDN domain is empty.", nameof(domain));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("ARVAN_API_KEY environment variable is missing.", nameof(apiKey));

            _apiBaseUrl = apiBaseUrl.TrimEnd('/');
            _domain = NormalizeDomain(domain);
            _client = new HttpClient();
            _client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization",
                apiKey.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase)
                    ? apiKey
                    : $"Apikey {apiKey}");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        public async Task InvalidateAsync(
            IReadOnlyCollection<string> objectKeys,
            CancellationToken cancellationToken = default)
        {
            if (objectKeys == null || objectKeys.Count == 0)
                return;

            var urls = new List<string>(objectKeys.Count);

            foreach (var objectKey in objectKeys)
            {
                var path = objectKey.Replace("\\", "/").TrimStart('/');
                urls.Add($"https://{_domain}/{EscapePath(path)}");
            }

            var body = JsonConvert.SerializeObject(new
            {
                purge = "individual",
                purge_urls = urls
            });

            var endpoint = $"{_apiBaseUrl}/domains/{Uri.EscapeDataString(_domain)}/caching";
            using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Arvan CDN cache purge failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private static string NormalizeDomain(string domain)
        {
            var value = domain.Trim();

            if (!value.Contains("://"))
                value = $"https://{value}";

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
                throw new ArgumentException($"Invalid Arvan CDN domain: {domain}", nameof(domain));

            return uri.Host;
        }

        private static string EscapePath(string path)
        {
            var parts = path.Split('/');

            for (var i = 0; i < parts.Length; i++)
                parts[i] = Uri.EscapeDataString(parts[i]);

            return string.Join("/", parts);
        }
    }
}
