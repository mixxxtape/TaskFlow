FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TaskFlow.Web/TaskFlow.Web.csproj TaskFlow.Web/
RUN dotnet restore TaskFlow.Web/TaskFlow.Web.csproj
COPY TaskFlow.Web/ TaskFlow.Web/
RUN dotnet publish TaskFlow.Web/TaskFlow.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskFlow.Web.dll"]
