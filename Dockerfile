FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 7002
ENV ASPNETCORE_URLS=http://+:7002

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/BasicFinance.Web/BasicFinance.Web.csproj", "src/BasicFinance.Web/"]
RUN dotnet restore "src/BasicFinance.Web/BasicFinance.Web.csproj"
COPY . .
WORKDIR "/src/src/BasicFinance.Web"
RUN dotnet publish "BasicFinance.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "BasicFinance.Web.dll"]
