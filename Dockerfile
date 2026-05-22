FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY financiera-backend-dotnet-feature-api-inicial/ApiEjemplo.csproj ./
RUN dotnet restore

COPY financiera-backend-dotnet-feature-api-inicial/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiEjemplo.dll"]
