FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BookingApi/BookingApi.csproj", "BookingApi/"]
RUN dotnet restore "BookingApi/BookingApi.csproj"

COPY . .
WORKDIR "/src/BookingApi"
RUN dotnet publish "BookingApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Render injects PORT at runtime; default to 8080 for local/compose use
ENV PORT=8080
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

# At startup, respect Render's injected PORT
CMD ASPNETCORE_HTTP_PORTS=$PORT dotnet BookingApi.dll
