# Use the official .NET runtime image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TicketingSystem.Ticketing/TicketingSystem.Ticketing.csproj", "src/TicketingSystem.Ticketing/"]
COPY ["src/TicketingSystem.Shared/TicketingSystem.Shared.csproj", "src/TicketingSystem.Shared/"]
RUN dotnet restore "src/TicketingSystem.Ticketing/TicketingSystem.Ticketing.csproj"
COPY . .
WORKDIR "/src/src/TicketingSystem.Ticketing"
RUN dotnet build "TicketingSystem.Ticketing.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TicketingSystem.Ticketing.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TicketingSystem.Ticketing.dll"]
