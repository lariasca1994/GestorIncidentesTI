# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY ["src/GestorIncidentesTI/GestorIncidentesTI.csproj", "src/GestorIncidentesTI/"]
RUN dotnet restore "src/GestorIncidentesTI/GestorIncidentesTI.csproj"
COPY . .
WORKDIR "/src/src/GestorIncidentesTI"
RUN dotnet build "GestorIncidentesTI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorIncidentesTI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
# Seguridad en cada compilación: actualizaciones del sistema (Linux), pip al día (sus
# CVE de octubre 2026) y almacén de certificados sin las raíces que firmaron los
# intermedios comprometidos de eMudhra (emSign y Comodo "AAA Certificate Services").
RUN if command -v apt-get > /dev/null; then \
        apt-get update && apt-get upgrade -y --no-install-recommends && rm -rf /var/lib/apt/lists/*; \
    fi \
    && if command -v pip > /dev/null; then pip install --no-cache-dir --upgrade pip > /dev/null; fi \
    && if command -v update-ca-certificates > /dev/null; then \
        rm -f /usr/share/ca-certificates/mozilla/emSign_* /usr/share/ca-certificates/mozilla/Comodo_AAA_Services_root.crt \
        && update-ca-certificates --fresh > /dev/null 2>&1; \
    fi
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorIncidentesTI.dll"]