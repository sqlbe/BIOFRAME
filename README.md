# BIOFRAME

동물·곤충 모티프 SF 생체 병기를 조립해 3인칭으로 조종하는 팀 아레나 게임.

- 엔진: Unity 6.3 LTS (6000.3.24f1) / URP
- 문서: `D:\바이브코딩\BIOFRAME\`
  - `bioframe-gdd.html` — 게임 기획서 (GDD v0.1)
  - `bioframe-parts-spec.html` — 파츠 규격서 (SPEC-PARTS v0.2)

## 폴더

```
Assets/_Project/
  Scripts/Rules      스탯·피해·시너지·조립 규칙 (Unity 비의존 순수 C#)
  Scripts/Assembly   파츠 조립, 모델 장착
  Scripts/Movement   이동 모드, 다리 IK, 카메라
  Scripts/Combat     공격 실행, 히트 판정, 락온
  Scripts/Match      라운드, 편성 단계, 승패
  Scripts/Net        네트워크 동기화
  Scripts/UI         조립 화면, 전투 HUD
  Data/              코어·파츠·시너지 JSON
  Prefabs/Parts      M1~M3는 박스 임시 파츠
  Tests/EditMode     Rules 자동 테스트
```

## 현재 단계

M1 조작 프로토타입 — 확인할 질문: "혼자 움직이기만 해도 재미있나?"
