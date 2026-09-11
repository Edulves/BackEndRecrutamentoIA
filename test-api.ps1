# Script de Testes para API de Ranking de Currículos
# Uso: powershell ./test-api.ps1

$baseUrl = "http://localhost:5105"
$username = "testuser"
$password = "Senha@123!"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Testes API Ranking de Currículos" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# TESTE 1: Registrar usuário
Write-Host "1️⃣  Registrando usuário..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl/api/auth/registrar" `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body (ConvertTo-Json @{ username = $username; password = $password }) `
        -ErrorAction SilentlyContinue
    
    Write-Host "✅ Usuário registrado com sucesso" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Usuário pode já existir (ou erro: $($_.Exception.Message))" -ForegroundColor Yellow
}

Write-Host ""

# TESTE 2: Login
Write-Host "2️⃣  Fazendo login..." -ForegroundColor Yellow
try {
    $loginResponse = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl/api/auth/login" `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body (ConvertTo-Json @{ username = $username; password = $password })
    
    $token = $loginResponse.token
    Write-Host "✅ Login realizado com sucesso" -ForegroundColor Green
    Write-Host "Token: $($token.Substring(0, 50))..." -ForegroundColor Gray
} catch {
    Write-Host "❌ Erro ao fazer login: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# TESTE 3: Listar candidatos
Write-Host "3️⃣  Listando candidatos..." -ForegroundColor Yellow
try {
    $candidatos = Invoke-RestMethod `
        -Method Get `
        -Uri "$baseUrl/api/candidatos" `
        -Headers @{ "Authorization" = "Bearer $token" }
    
    Write-Host "✅ Candidatos listados" -ForegroundColor Green
    Write-Host "Total: $($candidatos.Count)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Erro: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# TESTE 4: Analisar currículos
Write-Host "4️⃣  Analisando currículos (teste-a.txt e teste-b.txt)..." -ForegroundColor Yellow

$descricao = "Desenvolvedor Full Stack Pleno. Requisitos: C#, .NET, SQL Server, Azure, React, Python, Docker e AWS."

try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    $form = @{
        descricaoVaga = $descricao
        curriculos = @(
            Get-Item "RecrutamentoIA.Api/teste-a.txt",
            Get-Item "RecrutamentoIA.Api/teste-b.txt"
        )
    }
    
    $analise = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl/api/analisar" `
        -Headers @{ "Authorization" = "Bearer $token" } `
        -Form $form
    
    $stopwatch.Stop()
    
    Write-Host "✅ Análise concluída" -ForegroundColor Green
    Write-Host "Tempo: $($stopwatch.ElapsedMilliseconds) ms" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📊 Ranking de Candidatos:" -ForegroundColor Yellow
    
    foreach ($item in $analise.ranking) {
        Write-Host ""
        Write-Host "  Arquivo: $($item.nomeArquivo)" -ForegroundColor White
        Write-Host "  Candidato: $($item.nomeCandidato)" -ForegroundColor White
        Write-Host "  Score: $($item.score)/100" -ForegroundColor Cyan
        Write-Host "  Resumo: $($item.resumo)" -ForegroundColor Gray
        Write-Host "  Habilidades: $($item.habilidadesIdentificadas -join ', ')" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Processado em: $($analise.processadoEm)" -ForegroundColor Gray
    
} catch {
    Write-Host "❌ Erro na análise: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Detalhes: $($_)" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "✅ Testes Concluídos!" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
