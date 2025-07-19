# Use the official .NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TicketingSystem.Events/TicketingSystem.Events.csproj", "src/TicketingSystem.Events/"]
COPY ["src/TicketingSystem.Shared/TicketingSystem.Shared.csproj", "src/TicketingSystem.Shared/"]
RUN dotnet restore "src/TicketingSystem.Events/TicketingSystem.Events.csproj"
COPY . .
WORKDIR "/src/src/TicketingSystem.Events"
RUN dotnet build "TicketingSystem.Events.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TicketingSystem.Events.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TicketingSystem.Events.dll"]
