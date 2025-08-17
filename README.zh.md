# LFS MinIO/S3 传输代理

*[中文](README.zh.md) | [English](README.md)*

用于 MinIO 和 AWS S3 兼容存储的 Git LFS 自定义传输代理。

## 特性

- **双存储支持**: 同时支持 MinIO/S3 兼容端点和原生 AWS S3
- **智能传输**: AWS S3 大文件自动分片上传，使用 TransferUtility 优化
- **健壮的重试逻辑**: 指数退避算法，带抖动处理，处理瞬时故障
- **并发传输**: 可配置并发数，基于信号量的流控
- **进度报告**: 实时传输进度，符合 Git LFS 协议规范
- **连接验证**: 启动时连通性检查，提前发现配置问题

## 安装

### 快速安装

#### Windows (PowerShell)
```powershell
iwr -useb https://raw.githubusercontent.com/CruzLiu/LfsMinio/master/install.ps1 | iex
```

#### Linux/macOS (Bash)
```bash
curl -fsSL https://raw.githubusercontent.com/CruzLiu/LfsMinio/master/install.sh | bash
```

### 手动安装

1. 从 [GitHub Releases](https://github.com/CruzLiu/LfsMinio/releases/latest) 下载最新版本
2. 解压到您喜欢的目录（例如 `~/.lfs-mirror`）
3. 将该目录添加到系统 PATH

## 配置

### 环境变量

#### 存储配置

| 变量名 | 描述 | 示例值 |
|--------|------|--------|
| `LFS_S3_BUCKET` | S3 存储桶名称（必需） | `my-lfs-bucket` |
| `LFS_S3_ENDPOINT` | S3 兼容端点 URL | `https://minio.example.com:9000` |
| `LFS_S3_ACCESS_KEY` | 访问密钥 | `minioadmin` |
| `LFS_S3_SECRET_KEY` | 密钥 | `minioadmin` |
| `LFS_S3_SECURE` | 使用 HTTPS（默认: false） | `true`/`false` |
| `AWS_REGION` | AWS 区域 | `us-east-1` |

#### 传输配置

| 变量名 | 描述 | 默认值 |
|--------|------|--------|
| `LFS_RETRY_MAX_ATTEMPTS` | 最大重试次数 | `4` |
| `LFS_RETRY_BASE_MS` | 基础重试延迟（毫秒） | `300` |
| `LFS_RETRY_MAX_MS` | 最大重试延迟（毫秒） | `5000` |

### 存储模式选择

- **MinIO/S3 兼容模式**: 当设置 `LFS_S3_ENDPOINT` 时
- **AWS S3 模式**: 当 `LFS_S3_ENDPOINT` 为空时（使用 AWS 默认凭证链）

## Git LFS 设置

### 1. 配置 Git LFS 自定义传输

```bash
# 设置自定义传输代理
git -c lfs.customtransfer.lfs-s3.path="LfsMinio.exe" `
    -c lfs.standalonetransferagent=lfs-s3 `
    clone https://gitee.com/li_zhixin/lfs-test.git
    
git config lfs.customtransfer.lfs-s3.path LfsMinio.exe
git config lfs.standalonetransferagent lfs-s3
```

### 2. 环境配置

#### MinIO 示例
```bash
export LFS_S3_BUCKET=my-lfs-bucket
export LFS_S3_ENDPOINT=minio.example.com:9000
export LFS_S3_ACCESS_KEY=minioadmin
export LFS_S3_SECRET_KEY=minioadmin
export LFS_S3_SECURE=false 
```

#### AWS S3 示例
```bash
export LFS_S3_BUCKET=my-lfs-bucket
export AWS_REGION=us-east-1
# AWS 凭证通过环境变量、IAM 角色或 ~/.aws/credentials
# 方式 1: 使用 LFS_S3_* 变量
export LFS_S3_ACCESS_KEY=your-access-key
export LFS_S3_SECRET_KEY=your-secret-key
# 方式 2: 使用 AWS 默认凭证链（推荐）
# 如果使用 IAM 角色或 ~/.aws/credentials，无需额外设置
```

### 3. 使用方法

配置完成后，Git LFS 操作会透明工作：

```bash
git lfs track "*.psd"
git add large-file.psd
git commit -m "添加大文件"
git push origin main
```

## 构建

### 开发构建
```bash
dotnet build
```

## 高级配置

### 重试策略
```bash
export LFS_RETRY_MAX_ATTEMPTS=6      # 最多重试 6 次
export LFS_RETRY_BASE_MS=500         # 起始延迟 500ms
export LFS_RETRY_MAX_MS=30000        # 最大延迟 30 秒
```

### 日志记录
通过标准 .NET 日志环境变量设置日志级别：
```bash
export DOTNET_CONSOLE_LOGLEVEL=Debug  # 详细日志
```

## 错误处理

代理实现了全面的重试逻辑，支持：

### AWS S3 错误
- 5xx 服务器错误 (500, 502, 503, 504)
- 429 请求过多
- 408 请求超时
- 特定错误代码: `RequestTimeout`, `SlowDown`, `Throttling`, `InternalError`

### 网络错误
- 连接超时和重置
- DNS 解析失败
- Socket 异常
- HTTP 请求异常

### MinIO/S3 兼容错误
- 超时相关错误
- 连接和网络问题
- 服务器不可用

## 故障排除

1. **连接问题**: 代理在启动时会验证连通性
2. **身份验证**: 检查访问密钥和权限
3. **区域不匹配**: 确保为 S3 存储桶设置正确的 AWS 区域
4. **防火墙**: 验证对存储端点的网络访问
5. **日志**: 启用调试日志进行详细诊断

## 许可证

本项目采用 MIT 许可证。