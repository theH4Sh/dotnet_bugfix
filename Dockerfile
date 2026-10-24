FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BookingApi/BookingApi.csproj", "BookingApi/"]
RUN dotnet restore "BookingApi/BookingApi.csproj"

COPY . .
WORKDIR "/src/BookingApi"
RUN dotnet build "BookingApi.csproj" -c Release -o /app/build
RUN dotnet publish "BookingApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BookingApi.dll"]

