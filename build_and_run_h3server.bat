@echo off
REM This script builds the HTTP/3 Server Docker image and runs its h3server Docker container.


SET CONTAINER_NAME="h3server"
SET DOCKERFILE_DIR="Dockerfile.h3server.ubuntu"
SET IMAGE_NAME=h3server

docker build -t %IMAGE_NAME% --platform linux/amd64 -f %DOCKERFILE_DIR% .

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to build the Docker image.
    exit /b %ERRORLEVEL%
)

docker rm -f %CONTAINER_NAME% || true
docker run --name %CONTAINER_NAME%  -p 6001:6001 %IMAGE_NAME% -d

IF %ERRORLEVEL% NEQ 0 (
    echo Failed to run the Docker container.
    exit /b %ERRORLEVEL%
)

echo Docker container %CONTAINER_NAME% started successfully.
