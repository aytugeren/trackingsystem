# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
WORKDIR /src/Api
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl postgresql-client \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    PricingFeed__Enabled=false \
    PricingFeed__Url=http://www.dijikur.com/Odalar/Machine.aspx?v=5&m=D9FBE3028649CCD7BC3240CD20C59101 \
    PricingFeed__PricePath=price \
    PricingFeed__IntervalSeconds=300 \
    PricingFeed__TimeoutSeconds=10 \
    PricingFeed__UserEmail=dijikur@eren.com \
    PricingFeed__HttpMethod=GET \
    PricingFeed__MinimumPrice=0.01
ENTRYPOINT ["dotnet", "Api.dll"]
