#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/PodiumdAdapter.Web/PodiumdAdapter.Web.csproj", "PodiumdAdapter.Web/"]
RUN dotnet restore "./PodiumdAdapter.Web/PodiumdAdapter.Web.csproj"
COPY ["src/PodiumdAdapter.Web/*", "PodiumdAdapter.Web/"]
WORKDIR "/src/PodiumdAdapter.Web"
RUN dotnet build "./PodiumdAdapter.Web.csproj" -c $BUILD_CONFIGURATION  --no-restore -o /app/build

FROM build AS test
ARG BUILD_CONFIGURATION=Release
WORKDIR /test
COPY ["test/PodiumdAdapter.Web.Test/PodiumdAdapter.Web.Test.csproj", "PodiumdAdapter.Web.Test/"]
RUN dotnet restore "./PodiumdAdapter.Web.Test/PodiumdAdapter.Web.Test.csproj"
COPY ["test/PodiumdAdapter.Web.Test/*", "PodiumdAdapter.Web.Test/"]
WORKDIR "/test/PodiumdAdapter.Web.Test"
RUN dotnet build "./PodiumdAdapter.Web.Test.csproj" -c $BUILD_CONFIGURATION --no-restore
RUN dotnet test -c Release --no-build --logger trx --results-directory /testresults -v n; exit 0

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PodiumdAdapter.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS web
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PodiumdAdapter.Web.dll"]

FROM scratch as final
COPY --from=test /testresults/*.trx .
# this line only exists so we can build and cache all stages in one go
COPY --from=web dummy?.txt . 