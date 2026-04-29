using System.Runtime.CompilerServices;

// 스모크 테스트는 게임 어셈블리의 internal 스냅샷/검증 헬퍼에 접근한다.
// 게임 로직을 실행 파일과 분리한 뒤에도 기존 테스트를 그대로 유지하기 위해
// 테스트 어셈블리에만 내부 접근을 허용한다.
[assembly: InternalsVisibleTo("My2DEngine.SmokeTests")]
