# GitHub Actions CI/CD

This project includes GitHub Actions workflows for automated building, testing, and Docker image creation.

## Workflows

### 1. CI Workflow (`.github/workflows/ci.yml`)

Runs on every push and pull request:
- Builds the .NET 8 solution
- Publishes the application
- Uploads build artifacts

**Triggers:**
- Push to any branch
- Pull requests to any branch

### 2. Build and Test Workflow (`.github/workflows/build.yml`)

Comprehensive build and test workflow:
- **Build Job**: Builds the solution in Release configuration
- **Test Job**: Runs tests (if any test projects exist)
- **Docker Build Job**: Builds Docker image (only on main/master branch)

**Triggers:**
- Push to `main`, `master`, or `develop` branches
- Pull requests to `main`, `master`, or `develop` branches
- Manual workflow dispatch

### 3. Release Workflow (`.github/workflows/release.yml`)

Creates release artifacts and Docker images:
- Builds Docker image and pushes to GitHub Container Registry
- Creates platform-specific build artifacts (Linux, Windows)
- Tags images with version numbers

**Triggers:**
- When a new release is created
- Manual workflow dispatch with version input

### 4. Docker Build Workflow (`.github/workflows/docker.yml`)

Builds and pushes Docker images to GitHub Container Registry:
- Builds Docker image
- Pushes to `ghcr.io` (GitHub Container Registry)
- Tags with branch name, version, and SHA

**Triggers:**
- Push to `main` or `master` branches
- Push tags starting with `v*`
- Manual workflow dispatch

## Usage

### Automatic Builds

Workflows run automatically on:
- Push to main/master/develop branches
- Pull requests
- Release creation

### Manual Trigger

You can manually trigger workflows:
1. Go to **Actions** tab in GitHub
2. Select the workflow
3. Click **Run workflow**
4. Choose branch and click **Run workflow**

### Viewing Results

- Go to **Actions** tab to see workflow runs
- Click on a workflow run to see detailed logs
- Download artifacts from completed runs

## Docker Images

Docker images are automatically built and pushed to:
```
ghcr.io/<your-username>/<repository-name>:<tag>
```

Example:
```
ghcr.io/username/projeto-scraper:latest
ghcr.io/username/projeto-scraper:v1.0.0
ghcr.io/username/projeto-scraper:main
```

## Pulling Docker Images

```bash
# Login to GitHub Container Registry
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Pull the image
docker pull ghcr.io/<your-username>/<repository-name>:latest

# Run the container
docker run -p 9696:9696 ghcr.io/<your-username>/<repository-name>:latest
```

## Artifacts

Build artifacts are available for download:
- **published-app**: Published .NET application
- **media-scraper-{runtime}**: Platform-specific archives (Linux, Windows)

Artifacts are retained for:
- CI builds: 1 day
- Release builds: 30 days

## Environment Variables

Workflows use these environment variables:
- `DOTNET_VERSION`: `8.0.x`
- `SOLUTION_FILE`: `Scraper.sln`

## Secrets

No secrets are required for basic builds. For pushing to container registries:
- `GITHUB_TOKEN`: Automatically provided by GitHub Actions

## Customization

### Change .NET Version

Edit the `DOTNET_VERSION` in workflow files:
```yaml
env:
  DOTNET_VERSION: '8.0.x'  # Change to desired version
```

### Add Test Projects

If you add test projects, they will automatically be discovered and run:
```bash
dotnet test Scraper.sln
```

### Modify Docker Build

Edit `.github/workflows/docker.yml` to:
- Change registry
- Add additional tags
- Modify build arguments

## Troubleshooting

### Build Fails

1. Check the **Actions** tab for error logs
2. Verify .NET SDK version matches project requirements
3. Ensure all dependencies are restored

### Docker Build Fails

1. Check Dockerfile syntax
2. Verify all required files are present
3. Check Docker build logs in Actions

### Tests Fail

1. Review test output in Actions logs
2. Tests are set to `continue-on-error: true` so they won't fail the build
3. Remove `continue-on-error` if you want tests to block the build

