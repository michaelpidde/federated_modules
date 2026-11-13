podman build -t gateway -f PiddeCorp.Gateway/Dockerfile .
podman stop gateway 2> nul
podman rm gateway 2> nul
podman run -d --name gateway --network pidde-net -p 8082:8080 -e ASPNETCORE_ENVIRONMENT=Production gateway


podman build -t taskmanager.api -f PiddeCorp.TaskManager.API/Dockerfile .
podman stop taskmanager.api 2> nul
podman rm taskmanager.api 2> nul
podman run -d --name taskmanager.api --network pidde-net -p 8081:8080 -e ASPNETCORE_ENVIRONMENT=Production taskmanager.api


podman build -t taskmanager.ui -f PiddeCorp.TaskManager.UI/Dockerfile .
podman stop taskmanager.ui 2> nul
podman rm taskmanager.ui 2> nul
podman run -d --name taskmanager.ui --network pidde-net -p 5174:80 taskmanager.ui


podman build -t module2.api -f PiddeCorp.Module2.API/Dockerfile .
podman stop module2.api 2> nul
podman rm module2.api 2> nul
podman run -d --name module2.api --network pidde-net -p 8084:8080 -e ASPNETCORE_ENVIRONMENT=Production module2.api


podman build -t module2.ui -f PiddeCorp.Module2.UI/Dockerfile .
podman stop module2.ui 2> nul
podman rm module2.ui 2> nul
podman run -d --name module2.ui --network pidde-net -p 5176:80 module2.ui


podman build -t portal.shell -f PiddeCorp.Portal.Shell/Dockerfile .
podman stop portal.shell 2> nul
podman rm portal.shell 2> nul
podman run -d --name portal.shell --network pidde-net -p 5175:80 portal.shell

