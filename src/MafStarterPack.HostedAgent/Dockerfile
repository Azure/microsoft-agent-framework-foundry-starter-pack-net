# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

COPY . /src/MafStarterPack.HostedAgent

WORKDIR /src/MafStarterPack.HostedAgent

RUN dotnet publish -c Release -o /app --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

WORKDIR /app

COPY --from=build /app .

USER $APP_UID

EXPOSE 8088

ENTRYPOINT ["dotnet", "MafStarterPack.HostedAgent.dll"]
