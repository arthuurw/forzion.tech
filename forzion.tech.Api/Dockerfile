FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY forzion.tech.Domain/forzion.tech.Domain.csproj           forzion.tech.Domain/
COPY forzion.tech.Application/forzion.tech.Application.csproj forzion.tech.Application/
COPY forzion.tech.Infrastructure/forzion.tech.Infrastructure.csproj forzion.tech.Infrastructure/
COPY forzion.tech.Api/forzion.tech.Api.csproj                 forzion.tech.Api/
RUN dotnet restore forzion.tech.Api/forzion.tech.Api.csproj

COPY forzion.tech.Domain/      forzion.tech.Domain/
COPY forzion.tech.Application/ forzion.tech.Application/
COPY forzion.tech.Infrastructure/ forzion.tech.Infrastructure/
COPY forzion.tech.Api/         forzion.tech.Api/

RUN dotnet publish forzion.tech.Api/forzion.tech.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "forzion.tech.Api.dll"]
