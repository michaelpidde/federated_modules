@echo off

podman machine init --memory 12000 --cpus 8 --disk-size 60
podman machine start
podman network create pidde-net

podman run -d --name piddecorp-redis --network pidde-net --restart unless-stopped -p 6379:6379 docker.io/redis:7-alpine redis-server --save "" --appendonly no
