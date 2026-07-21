# AWS-Uploader
Upload Unity Addressables to S3 compatible storage.

# Installation

## Install via Unity Package Manager (UPM)

1. Open your Unity project.
2. Go to **Window → Package Manager**.
3. Click the **+** button in the top-left corner.
4. Select **Add package from git URL...**
5. Enter the repository URL:

```text
https://github.com/salehb02/AWS-Uploader.git
```

Or install a specific version:

```text
https://github.com/salehb02/AWS-Uploader.git#1.0.0
```

6. Click **Add** and wait for Unity to import the package.

---

## Requirements

- Unity 2022.3 or newer
- .NET Standard 2.1 compatible scripting runtime
- AWS S3 compatible Object Storage
  - Amazon S3
  - ArvanCloud Object Storage
  - MinIO
  - Cloudflare R2
  - Backblaze B2 S3
  - Any S3-compatible provider

---

## Verify Installation

After the package is installed, the uploader window will be available from:

```
Tools → DevDude → Addressables Uploader
```
