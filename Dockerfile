FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Listmate.Domain/ src/Listmate.Domain/
COPY src/Listmate.Application/ src/Listmate.Application/
COPY src/Listmate.Infrastructure/ src/Listmate.Infrastructure/
COPY src/Listmate.Api/ src/Listmate.Api/
RUN --mount=type=cache,id=listmate-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Listmate.Api/Listmate.Api.csproj -c Release -o /app \
      -p:UseAppHost=false -nodeReuse:false -p:UseSharedCompilation=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x4B
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Listmate.Api.dll"]
