# 아웃게임 저장·복원 Inspector 테스트

팀의 실제 DTO와 매니저가 준비되기 전 `ISaveDataProvider<T>`와 공통 JSON 저장 경로를 검증한다.
실제 재화·특성·제단 효과를 변경하지 않는다. `test_`로 시작하는 ID는 가상 테스트 데이터다.

## 실행

1. 테스트 씬의 빈 GameObject에 `OutGameSaveTestPanel`을 추가한다. `SaveManager`도 자동으로 추가된다.
2. Play 모드에서 컴포넌트 우측 메뉴(⋮)의 **Save Test → 1. Apply Sample Data**를 실행한다.
3. **2. Save To JSON**을 실행한다. Console에 실제 파일 경로가 표시된다.
4. **3. Clear Memory Only**로 State를 비운다.
5. **4. Load And Restore**를 실행한다. 혈석 500, 해금 제단 2개, 특성 레벨 2, 선택 제단과 토템 레벨 3이 복원되어야 한다.
6. 4번을 다시 실행해도 재화와 목록이 중복 증가하지 않아야 한다.
7. Play 모드를 종료하고 다시 실행한 뒤 **4. Load And Restore**만 실행해 파일에서 복원되는지 확인한다.

State를 Inspector에서 직접 수정한 뒤 저장·복원해도 된다. 자동 저장과 자동 로드는 하지 않는다.
Play 모드 밖에서도 메뉴는 동작하지만 상태 변경은 테스트 용도로만 사용한다.

## 테스트 데이터

- `Permanent`: 영구 재화인 혈석, 해금 제단 ID, 특성 ID·레벨.
- `LastSelection`: 마지막으로 선택한 제단 ID와 토템 ID·레벨. 영구 성장과 구분된 선택 복원 실험이다.
- 재화/레벨 음수, 중복/빈 ID, 미해금 제단 선택, 필수 목록 누락은 저장/복원 전에 거부한다.

저장 파일은 `Application.persistentDataPath/Saves/outgame-test-profile.json`이다.
런 진행이나 실제 플레이어 프로필 파일과 다른 테스트 전용 키를 사용한다.
파일이 없거나 손상되면 실패 사유를 표시하고 현재 State와 파일은 그대로 유지한다.

## 연결 계약

- `SaveKey<T>`는 파일 키와 DTO 타입, 버전을 묶는다.
- `SaveManager.TrySave(key, data, out error)`는 파일 기록/교체까지 성공해야 true를 반환한다.
- `SaveManager.TryLoad(key, out data, out error)`는 키·버전·타입을 확인한다. 콘텐츠 값 검증은 Provider가 담당한다.
- `CaptureSaveData` / `RestoreSaveData`는 목록을 복사해 저장 스냅샷과 실행 상태를 분리한다.
- 루트 DTO는 `[Serializable]` 구체 클래스로 정의하고 키의 타입과 실제 타입을 일치시킨다. List 등의 컬렉션은 루트 DTO의 필드로 감싼다.
- 중첩 데이터도 직렬화 가능한 필드로 정의한다. SO/컴포넌트, Dictionary, 프로퍼티, 임의의 다형 객체는 저장 필드로 사용하지 않는다. 콘텐츠별 Provider에서 필수 값 유효성을 검증한다.
- 테스트 Provider를 실제 재화·특성 Provider로 교체한 뒤 Coordinator에서 데이터를 모으면 같은 저장 경로를 사용할 수 있다.

## 자동 검증

Unity 메뉴 **Tools → OutGame → Validate Save Test**에서 파일 저장/복원과 실패 경로를 검증한다.
검증은 프로젝트 `Temp`의 별도 디렉터리와 임시 Preview Scene을 사용한다.
결과는 `Temp/OutGameSaveTestValidation.result.json`에 기록된다.
