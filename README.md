# LFS MinIO/S3 Transfer Agent

*[English](README.md) | [中文](README.zh.md)*

A Git LFS custom transfer agent for MinIO and AWS S3 compatible storage.

## Features

- **Dual Storage Support**: Works with both MinIO/S3-compatible endpoints and native AWS S3
- **Smart Transfer**: Automatic multipart upload for large files on AWS S3 with TransferUtility
- **Robust Retry Logic**: Exponential backoff with jitter for transient failures
- **Concurrent Transfers**: Configurable concurrency with semaphore-based throttling
- **Progress Reporting**: Real-time transfer progress with Git LFS protocol compliance
- **Connection Validation**: Startup connectivity checks to catch configuration issues early

## Installation

### Quick Install

#### Windows (PowerShell)
```powershell
iwr -useb https://raw.githubusercontent.com/CruzLiu/LfsMinio/master/install.ps1 | iex
```

#### Linux/macOS (Bash)
```bash
curl -fsSL https://raw.githubusercontent.com/CruzLiu/LfsMinio/master/install.sh | bash
```

### Manual Installation

1. Download the latest release from [GitHub Releases](https://github.com/CruzLiu/LfsMinio/releases/latest)
2. Extract to your preferred directory (e.g., `~/.lfs-mirror`)
3. Add the directory to your system PATH

## Configuration

### Environment Variables

#### Storage Configuration

| Variable | Description                | Example |
|----------|----------------------------|----------|
| `LFS_S3_BUCKET` | S3 bucket name (required)  | `my-lfs-bucket` |
| `LFS_S3_ENDPOINT` | S3-compatible endpoint URL | `https://minio.example.com:9000` |
| `LFS_S3_ACCESS_KEY` | Access key                 | `minioadmin` |
| `LFS_S3_SECRET_KEY` | Secret key                 | `minioadmin` |
| `LFS_S3_SECURE` | Use HTTPS (default: false)    | `true`/`false` |
| `AWS_REGION` | AWS region                 | `us-east-1` |

#### Transfer Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `LFS_RETRY_MAX_ATTEMPTS` | Max retry attempts | `4` |
| `LFS_RETRY_BASE_MS` | Base retry delay (ms) | `300` |
| `LFS_RETRY_MAX_MS` | Max retry delay (ms) | `5000` |

### Storage Mode Selection

- **MinIO/S3-Compatible Mode**: When `LFS_S3_ENDPOINT` is set
- **AWS S3 Mode**: When `LFS_S3_ENDPOINT` is empty (uses AWS default credential chain)

## Git LFS Setup

### 1. Configure Git LFS Custom Transfer

```bash
git -c lfs.customtransfer.lfs-s3.path="LfsMinio.exe" `
    -c lfs.standalonetransferagent=lfs-s3 `
    clone https://gitee.com/li_zhixin/lfs-test.git
    
git config lfs.customtransfer.lfs-s3.path LfsMinio.exe
git config lfs.standalonetransferagent lfs-s3
```

### 2. Environment Setup

#### MinIO Example
```bash
export LFS_S3_BUCKET=my-lfs-bucket
export LFS_S3_ENDPOINT=minio.example.com:9000
export LFS_S3_ACCESS_KEY=minioadmin
export LFS_S3_SECRET_KEY=minioadmin
export LFS_S3_SECURE=false
```

#### AWS S3 Example
```bash
export LFS_S3_BUCKET=my-lfs-bucket
export AWS_REGION=us-east-1
# AWS credentials via environment, IAM role, or ~/.aws/credentials
# Option 1: Use LFS_S3_* variables
export LFS_S3_ACCESS_KEY=your-access-key
export LFS_S3_SECRET_KEY=your-secret-key
# Option 2: Use AWS default credential chain (recommended)
# No additional setup needed if using IAM roles or ~/.aws/credentials
```

### 3. Usage

Once configured, Git LFS operations work transparently:

```bash
git lfs track "*.psd"
git add large-file.psd
git commit -m "Add large file"
git push origin main
```

## Building

### Development Build
```bash
dotnet build
```

## Advanced Configuration

### Retry Policy
```bash
export LFS_RETRY_MAX_ATTEMPTS=6      # Try up to 6 times
export LFS_RETRY_BASE_MS=500         # Start with 500ms delay
export LFS_RETRY_MAX_MS=30000        # Cap at 30 seconds
```

### Logging
Set log level via standard .NET logging environment variables:
```bash
export DOTNET_CONSOLE_LOGLEVEL=Debug  # For detailed logging
```

## Error Handling

The agent implements comprehensive retry logic for:

### AWS S3 Errors
- 5xx server errors (500, 502, 503, 504)
- 429 Too Many Requests
- 408 Request Timeout
- Specific error codes: `RequestTimeout`, `SlowDown`, `Throttling`, `InternalError`

### Network Errors
- Connection timeouts and resets
- DNS resolution failures  
- Socket exceptions
- HTTP request exceptions

### MinIO/S3-Compatible Errors
- Timeout-related errors
- Connection and network issues
- Server unavailability

## Troubleshooting

1. **Connection Issues**: The agent validates connectivity on startup
2. **Authentication**: Check access keys and permissions
3. **Region Mismatch**: Ensure correct AWS region for S3 buckets
4. **Firewall**: Verify network access to storage endpoints
5. **Logs**: Enable debug logging for detailed diagnostics

## License

This project is licensed under the MIT License.