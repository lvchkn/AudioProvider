## AudioProvider
This is a Telegram bot that takes in a link to a YouTube video that you own the rights for and converts it to an MP3 file.

#### Build the Docker image: 
- For ARM64 architecture:
  ```bash
  docker buildx build --platform linux/arm64 -t audioprovider .
  ```

- For AMD64 architecture:
  ```bash
  docker buildx build --platform linux/amd64 -t audioprovider .
  ```

-  Multi-platform image (only works when these [requirements](https://docs.docker.com/build/building/multi-platform/#prerequisites) are met):
    ```bash
    docker buildx build --platform linux/arm64,linux/amd64 -t audioprovider .
    ```

#### Run the container:
```bash
docker run \
    -e TELEGRAM_API_TOKEN= \
    -e ALLOWED_USER_ID= \
    --rm --name audioprovider -it \
    audioprovider
```
