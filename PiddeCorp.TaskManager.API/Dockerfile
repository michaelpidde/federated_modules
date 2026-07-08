FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching on restore
COPY ["PiddeCorp.TaskManager.API/PiddeCorp.TaskManager.API.csproj", "PiddeCorp.TaskManager.API/"]

RUN dotnet restore "PiddeCorp.TaskManager.API/PiddeCorp.TaskManager.API.csproj"

# Copy everything else
COPY . .

RUN dotnet publish "PiddeCorp.TaskManager.API/PiddeCorp.TaskManager.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PiddeCorp.TaskManager.API.dll"]