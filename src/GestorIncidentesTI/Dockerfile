# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GestorIncidentesTI/GestorIncidentesTI.csproj", "src/GestorIncidentesTI/"]
RUN dotnet restore "src/GestorIncidentesTI/GestorIncidentesTI.csproj"
COPY . .
WORKDIR "/src/src/GestorIncidentesTI"
RUN dotnet build "GestorIncidentesTI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorIncidentesTI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorIncidentesTI.dll"]