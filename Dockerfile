# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY ["src/GestorIncidentesTI/GestorIncidentesTI.csproj", "src/GestorIncidentesTI/"]
RUN dotnet restore "src/GestorIncidentesTI/GestorIncidentesTI.csproj"
COPY . .
WORKDIR "/src/src/GestorIncidentesTI"
RUN dotnet build "GestorIncidentesTI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorIncidentesTI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
# Almacén de certificados del sistema sin las raíces que firmaron los intermedios
# comprometidos de eMudhra (octubre 2026): emSign y Comodo "AAA Certificate Services".
RUN if command -v update-ca-certificates > /dev/null; then \
        rm -f /usr/share/ca-certificates/mozilla/emSign_* /usr/share/ca-certificates/mozilla/Comodo_AAA_Services_root.crt \
        && update-ca-certificates --fresh > /dev/null 2>&1; \
    fi
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorIncidentesTI.dll"]