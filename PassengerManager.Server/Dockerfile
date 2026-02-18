# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PassengerManager.Server/PassengerManager.Server.csproj", "PassengerManager.Server/"]
COPY ["PassengerManager.Shared/PassengerManager.Shared.csproj", "PassengerManager.Shared/"]

RUN dotnet restore "PassengerManager.Server/PassengerManager.Server.csproj"

COPY . .

WORKDIR "/src/PassengerManager.Server"
RUN dotnet publish "PassengerManager.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Standard .NET container port
EXPOSE 8080

ENTRYPOINT ["dotnet", "PassengerManager.Server.dll"]