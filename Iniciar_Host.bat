@echo off
title Jaguares Swim - Host combinado (API + Web)
echo ===================================================
echo   Iniciando Jaguares Swim (host combinado)
echo   API + Frontend en el mismo origen
echo   -^> http://localhost:5080
echo ===================================================
dotnet run --project src\Jaguares.Host
