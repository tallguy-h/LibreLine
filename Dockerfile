# Libre Line: build your own image with `docker build -t libreline .` (no image is published).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore LibreLine.slnx
RUN dotnet test LibreLine.slnx --configuration Release --no-restore
RUN dotnet publish src/LibreLine.Host/LibreLine.Host.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data && chown app:app /data
ENV LibreLine__DataDirectory=/data \
    LibreLine__Port=2445
USER app
VOLUME /data
EXPOSE 2445
ENTRYPOINT ["dotnet", "LibreLine.Host.dll"]
