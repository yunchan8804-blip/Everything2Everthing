@echo off
chcp 65001 > nul
:: Everything2Everything 1-클릭 자동 설치기 (TrustedPeople 인증서 신뢰 등록 및 MSIX 패키지 설치)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
