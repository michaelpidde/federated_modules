FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching on restore
COPY ["PiddeCorp.Module2.API/PiddeCorp.Module2.API.csproj", "PiddeCorp.Module2.API/"]

RUN dotnet restore "PiddeCorp.Module2.API/PiddeCorp.Module2.API.csproj"

# Copy everything else
COPY . .

RUN dotnet publish "PiddeCorp.Module2.API/PiddeCorp.Module2.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PiddeCorp.Module2.API.dll"]