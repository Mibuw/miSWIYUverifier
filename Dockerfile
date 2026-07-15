# Web app image (built locally on the deployment host — never pushed to a
# registry, see README "No Docker image of this project").
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/miSWIYUverifier/miSWIYUverifier.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# Kestrel port comes from appsettings.json (http://0.0.0.0:5070)
EXPOSE 5070
ENTRYPOINT ["dotnet", "miSWIYUverifier.dll"]
