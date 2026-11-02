FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY NuGet.Config Directory.Build.props global.json ./
COPY src/ src/
RUN dotnet restore src/NextAZ.Harness.Api/NextAZ.Harness.Api.csproj --configfile NuGet.Config --locked-mode
RUN dotnet publish src/NextAZ.Harness.Api/NextAZ.Harness.Api.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish ./
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "NextAZ.Harness.Api.dll"]
