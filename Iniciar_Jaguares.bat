@echo off
title Jaguares Swim - API + Frontend
echo ============================================
echo   Iniciando Jaguares Swim (desarrollo local)
echo   API      -^> http://localhost:5105
echo   Frontend -^> http://localhost:5000
echo ============================================

start "Jaguares API" cmd /k "dotnet run --project src\Jaguares.Api"
start "Jaguares Web" cmd /k "dotnet run --project src\Jaguares.Web"
