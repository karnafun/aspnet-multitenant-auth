# Base image with ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80 443

# Install curl and openssl for health checks and cert generation
RUN apt-get update && \
    apt-get install -y curl openssl && \
    rm -rf /var/lib/apt/lists/*

# SDK for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Install openssl in build stage
RUN apt-get update && apt-get install -y openssl && rm -rf /var/lib/apt/lists/*

# Copy certificate configuration
COPY cert.conf /tmp/cert.conf

# Generate proper HTTPS certificate with correct SANs
RUN mkdir -p /https && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /https/aspnetapp.key \
    -out /https/aspnetapp.crt \
    -config /tmp/cert.conf -extensions v3_req && \
    openssl pkcs12 -export \
    -out /https/aspnetapp.pfx \
    -inkey /https/aspnetapp.key \
    -in /https/aspnetapp.crt \
    -passout pass:DevCertPassword

COPY ["AuthMastery.API.csproj", "./"]
RUN dotnet restore "./AuthMastery.API.csproj"
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Copy certificate from build stage
COPY --from=build /https /https

ENTRYPOINT ["dotnet", "AuthMastery.API.dll"]