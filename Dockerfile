# syntax=docker/dockerfile:1
# Build the OpenLethe server. Static-data JSON is embedded in OpenLethe.Resources.dll,
# so the published output is self-contained (no loose data files to copy).

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cache-friendly): copy only the project files, restore, then copy the rest.
COPY src/OpenLethe.Packets/OpenLethe.Packets.csproj   src/OpenLethe.Packets/
COPY src/OpenLethe.Data/OpenLethe.Data.csproj         src/OpenLethe.Data/
COPY src/OpenLethe.Resources/OpenLethe.Resources.csproj src/OpenLethe.Resources/
COPY src/OpenLethe.Server/OpenLethe.Server.csproj     src/OpenLethe.Server/
RUN dotnet restore src/OpenLethe.Server/OpenLethe.Server.csproj

COPY src/ src/
# OpenLethe.Packets compiles ../../packets/**/*.cs (client-extracted types), a
# root-level folder outside src/ - it must be present or the server won't compile.
COPY packets/ packets/
RUN dotnet publish src/OpenLethe.Server/OpenLethe.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# aspnet base image runs as a non-root user, so bind to a high port.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenLethe.Server.dll"]
