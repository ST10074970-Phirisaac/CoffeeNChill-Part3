# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS installer-env
WORKDIR /src
COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
    dotnet publish CoffeeNChill.Functions.csproj --output /home/site/wwwroot

# Stage 2: Runtime
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]