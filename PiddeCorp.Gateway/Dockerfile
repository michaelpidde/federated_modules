FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching on restore
COPY ["PiddeCorp.Gateway/PiddeCorp.Gateway.csproj", "PiddeCorp.Gateway/"]

RUN dotnet restore "PiddeCorp.Gateway/PiddeCorp.Gateway.csproj"

# Copy everything else
COPY . .

RUN dotnet publish "PiddeCorp.Gateway/PiddeCorp.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PiddeCorp.Gateway.dll"]