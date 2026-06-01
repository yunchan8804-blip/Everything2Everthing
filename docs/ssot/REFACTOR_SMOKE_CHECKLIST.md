# 리팩토링 진입점 수동 스모크 체크리스트

> 자동 테스트로 덮이지 않는 진입점/UI 경로의 회귀 방어선. **DI 컴포지션 루트 전환(P2), MVVM 분해(P5) 직후 각 1회씩 강제 실행.**
> 적대적 리뷰 지적: `App.xaml.cs`는 진입점이 5개이고 `Environment.Exit` 즉시 종료·MSIX 쓰기폴더 제약이 있어 Host 라이프사이클 전환 시 충돌 지점이 많은데 자동 테스트가 0개다.

## 진입점 (App.xaml.cs / CliRouter)

- [ ] **ShowMain** — 인자 없이 실행 → 메인 창이 정상 표시되고 큐가 비어 있음
- [ ] **Quick (빠른 변환)** — 셸 컨텍스트 메뉴/CLI로 파일 1개 빠른 변환 → QuickProgressWindow가 제목 동적+% 갱신하며 뜨고, 완료 후 정상 종료(Environment.Exit 경로)
- [ ] **Dialog (변환 대화상자)** — 큐에 파일 추가 → 출력 형식 선택 → 변환 실행 → 결과 산출
- [ ] **Register** — `Everything2Everything.exe register` → 셸 컨텍스트 메뉴 등록 성공
- [ ] **Unregister** — `Everything2Everything.exe unregister` → 등록 해제 성공
- [ ] **Diagnose** — 진단 창이 외부 도구(FFmpeg/LibreOffice) 가용성 상태를 정확히 표시

## 셸 / 패키징

- [ ] **셸 컨텍스트 메뉴 initialFiles** — 탐색기에서 파일 선택 후 카스케이드 메뉴 → 추천 출력 목록이 입력 형식에 맞게 노출, 선택 시 변환
- [ ] **MSIX 쓰기가능 폴더** — MSIX 패키징 환경에서 임시 작업폴더/설정 저장이 쓰기 가능 경로를 사용(가상화 폴더 권한 문제 없음)

## 핵심 변환 동작 (P0~P6 내내 GUI 재확인)

- [ ] 이미지: png↔jpg, png→webp, 이미지 N장 → 단일 PDF/TIFF 결합
- [ ] 벡터/데이터: svg→png, csv↔json, csv↔xlsx
- [ ] PDF: pdf 압축(pdf→pdf), pdf→이미지
- [ ] 외부 도구(있을 때): 영상↔영상/오디오(FFmpeg), HWP/DOCX→PDF(LibreOffice), OCR
- [ ] AI(키 설정 시): txt 요약/번역/교정

## MVVM 분해(P5) 슬라이스별 추가 확인

- [ ] 미리보기 상태기계(로딩/성공/실패 전환)
- [ ] 출력 형식 자동 필터링(큐 교집합) 갱신
- [ ] 드래그앤드롭 큐 추가
- [ ] 진행률 클램프(0~100%) 및 취소
- [ ] 이력(History) 기록/표시
