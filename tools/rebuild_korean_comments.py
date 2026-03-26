from pathlib import Path
import re

ROOT = Path(r"C:\Users\PC\Desktop\YD_Unity\Stone_Finite")
ASSETS = ROOT / "Assets"
FILES = list(ASSETS.rglob("*.cs"))

DIRECT = {
    "IInventoryOwner": "인벤토리를 제공하는 객체가 구현하는 공용 인터페이스",
    "IInventoryOwnerConsumer": "인벤토리 소유자를 주입받는 객체가 구현하는 공용 인터페이스",
    "InventoryData": "인벤토리 슬롯 상태와 아이템 조작 로직을 담는 데이터 모델",
    "ItemData": "아이템 하나의 상태와 속성을 담는 데이터 모델",
    "WorldData": "월드 셀, 유체, 조명 상태를 저장하는 데이터 컨테이너",
    "Debugger": "런타임 디버그 정보를 화면에 표시하는 도구",
    "CellLibrary": "월드 셀 정의를 보관하고 조회하는 라이브러리",
    "ItemLibrary": "아이템 정의를 보관하고 생성하는 라이브러리",
    "RecipeLibrary": "제작 레시피를 로드하고 매칭하는 라이브러리",
    "BackGround": "배경 레이어를 카메라 기준으로 갱신하는 컴포넌트",
    "CameraFollow": "카메라가 플레이어를 따라가도록 갱신하는 컴포넌트",
    "Player": "플레이어의 핵심 상태와 외부 진입점을 담는 메인 컴포넌트",
    "InteractionController": "플레이어 상호작용 입력을 받아 서비스로 위임하는 진입점",
    "PlatformDropThroughService": "일방향 플랫폼 통과 처리를 담당하는 서비스",
    "CorpseHoverQueryService": "마우스 위치의 시체 hover 대상을 찾는 쿼리 서비스",
    "CombatHitSensor": "근접 공격 히트박스의 트리거 이벤트를 전달하는 센서",
    "FluidProbe": "플레이어가 유체 안에 있는지 감지하는 센서",
    "GroundProbe": "플레이어의 접지 상태를 감지하는 센서",
    "PickupSensor": "드랍 아이템 줍기 트리거를 전달하는 센서",
    "AudioManager": "효과음을 재생하는 지원 컴포넌트",
    "Cursor": "커서 아이콘과 툴팁 표시를 담당하는 UI 컴포넌트",
    "Hotbar": "핫바 슬롯 표시와 선택 상태를 관리하는 UI 컴포넌트",
    "ItemSlot": "아이템 슬롯 하나의 표시와 입력을 담당하는 UI 컴포넌트",
    "PlayerInventory": "플레이어 인벤토리 패널을 제어하는 UI 컴포넌트",
    "Heart": "체력 하트 하나의 표시를 담당하는 UI 컴포넌트",
    "ModuleSlotSyncUtility": "제작 모듈 UI의 슬롯 동기화 보조 로직을 담은 유틸리티",
    "CrucibleSmelts": "크루서블 제련 조합 정보를 표현하는 보조 타입",
    "CrucibleView": "크루서블 내부 조성 시각화를 담당하는 UI 컴포넌트",
    "ICursorTooltipSource": "커서 툴팁 내용을 제공하는 인터페이스",
    "VfxManager": "월드 시각효과를 생성하고 관리하는 지원 컴포넌트",
    "RotatingVfx": "회전하는 기어 시각효과를 갱신하는 컴포넌트",
    "BeltVfx": "벨트 시각효과를 갱신하는 컴포넌트",
    "GodRayBG": "배경 고드레이 효과를 제어하는 컴포넌트",
    "MultiBlockLibrary": "멀티블록 정의를 보관하고 조회하는 라이브러리",
    "UtilityLibrary": "월드 유틸리티 정의를 보관하고 조회하는 라이브러리",
    "DroppedItem": "월드에 떨어진 아이템 엔티티를 표현하는 컴포넌트",
    "Corpse": "상호작용 가능한 시체 엔티티를 표현하는 컴포넌트",
    "Cow": "소 몹의 외형과 행동을 담당하는 엔티티 컴포넌트",
    "Mob": "체력과 저장 기능을 갖는 몹 엔티티의 기본 클래스",
    "Entity": "월드에 저장 가능한 엔티티의 공통 기반 클래스",
    "EntityManager": "월드 엔티티를 등록하고 관리하는 매니저",
    "CorpseLibrary": "시체 프리팹과 가공 결과를 관리하는 팩토리",
    "ItemDropper": "아이템 드랍 엔티티 생성을 담당하는 팩토리",
    "MobLibrary": "몹 프리팹 등록과 스폰을 담당하는 팩토리",
    "WorldEntityFactory": "월드 엔티티 생성 경로를 한곳으로 모은 팩토리",
    "WorldEntityRestoreService": "세이브 데이터에서 월드 엔티티를 복구하는 서비스",
    "Multiblock": "모든 멀티블록이 공유하는 공통 상태와 동작을 담는 베이스 클래스",
    "MultiblockManager": "월드의 멀티블록 생성, 조회, 저장 연결을 담당하는 매니저",
    "Chunk": "월드 청크 하나의 타일맵과 렌더러 묶음을 표현하는 컴포넌트",
    "FallingBlock": "중력으로 떨어진 뒤 월드 셀로 되돌아가는 블록 엔티티",
    "BeltLink": "기어 사이 벨트 연결 정보를 표현하는 타입",
    "GearNetwork": "기어 네트워크 한 묶음의 계산 결과를 보관하는 타입",
    "GearNetworkManager": "기어 네트워크의 구성과 동력 계산을 담당하는 매니저",
    "GearNode": "월드에 배치된 기어 노드 하나를 표현하는 타입",
    "SourceNode": "기어 네트워크에 동력을 공급하는 소스 노드를 표현하는 타입",
    "ProceduralUtil": "절차 생성에서 공통으로 쓰는 계산 유틸리티 모음",
    "StructureTemplate": "구조물 배치용 템플릿 데이터를 표현하는 타입",
    "WorldDataGenerator": "새 월드 데이터를 절차 생성하는 생성기",
    "WorldGenSettings": "월드 생성 파라미터를 담는 설정 오브젝트",
    "EntityPersistence": "엔티티 저장 파일 기록을 담당하는 persistence 클래스",
    "PlayerPersistence": "플레이어 저장 파일 기록과 복원을 담당하는 persistence 클래스",
    "WorldBinaryMapPersistence": "월드 맵 바이너리 파일 저장과 로드를 담당하는 persistence 클래스",
    "WorldChunkSystem": "월드 청크 생성과 타일 갱신을 담당하는 시스템",
    "WorldSavePathResolver": "세이브 파일 경로를 계산하는 보조 클래스",
    "WorldSaveSystem": "월드 저장과 로드 진입점을 제공하는 facade",
    "WorldManager": "월드 시스템을 조립하고 외부에 기능을 노출하는 중심 매니저",
    "LobyManager": "로비 화면과 월드 선택 흐름을 제어하는 매니저",
    "WorldLoadContext": "로비에서 게임 씬으로 넘기는 월드 로드 문맥을 보관하는 컨텍스트",
    "ImageGenerator": "맵 프리뷰 이미지를 생성하는 도구",
    "GridGizmo": "테스트용 그리드 기즈모를 그리는 디버그 컴포넌트",
    "ClassDiagramGenerator": "코드 구조를 기반으로 클래스 다이어그램을 생성하는 에디터 도구",
    "ScriptSelectionManager": "다이어그램 생성 대상 스크립트 선택을 관리하는 에디터 도구",
}

SUFFIX = {
    "Loading": "정의 데이터를 읽고 캐시를 초기화하는 파일",
    "Lookup": "정의와 데이터를 조회하는 파일",
    "Rendering": "렌더링과 시각 표현을 담당하는 파일",
    "Factory": "객체 생성 책임을 분리한 파일",
    "Alloy": "합금 계산을 담당하는 파일",
    "Crafting": "제작 계산과 실행을 담당하는 파일",
    "Expressions": "표현식 평가를 담당하는 파일",
    "OutputActions": "제작 결과 액션 적용을 담당하는 파일",
    "Toolbench": "툴벤치 레시피 처리를 담당하는 파일",
    "Lifecycle": "Unity 생명주기와 초기화를 담당하는 파일",
    "Movement": "이동과 방향 갱신을 담당하는 파일",
    "Status": "체력, 자원, UI 상태 갱신을 담당하는 파일",
    "Build": "설치와 파괴 상호작용 진입점을 담당하는 파일",
    "Combat": "전투 상호작용을 담당하는 파일",
    "Hover": "hover와 하이라이트 처리를 담당하는 파일",
    "UI": "UI 연결과 화면 전환을 담당하는 파일",
    "Sync": "UI 동기화와 스냅샷 갱신을 담당하는 파일",
    "Candidates": "후보 목록 구성과 선택 로직을 담당하는 파일",
    "PersistenceHelpers": "저장과 복원 공통 보조 로직을 담은 파일",
    "Persistence": "저장과 복원 로직을 담당하는 파일",
    "Query": "조회용 facade 메서드를 모아둔 파일",
    "QueryService": "실제 조회 로직을 담당하는 파일",
    "ServiceContext": "서비스가 공유하는 런타임 의존성을 묶는 파일",
    "Sources": "동력원 등록과 해제를 담당하는 파일",
    "Belts": "벨트 연결과 해제를 담당하는 파일",
    "Networks": "기어 네트워크 계산 본체를 담는 파일",
    "Rebuild": "기어 네트워크 재구성을 담당하는 파일",
    "Vfx": "시각효과 갱신을 담당하는 파일",
    "BuildCommon": "공통 지형 데이터 생성을 담당하는 파일",
    "Generate": "월드 생성 진입점을 담당하는 파일",
    "Decor": "장식물과 자연 배치를 담당하는 파일",
    "Surface": "지표면과 표면 바이옴 생성을 담당하는 파일",
    "Volcano": "화산 지형 생성을 담당하는 파일",
    "Lighting": "조명 계산을 담당하는 파일",
    "Collision": "충돌과 접촉 반응을 담당하는 파일",
    "Health": "체력과 사망 처리를 담당하는 파일",
    "Behaviour": "행동과 연출 로직을 담당하는 파일",
}

CONTROL_PREFIXES = ("if ", "for ", "foreach ", "while ", "switch ", "catch ", "using ", "return ", "lock ")
METHOD_RE = re.compile(
    r"^\s*(?:(?:public|private|protected|internal)\s+)?"
    r"(?:(?:static|virtual|override|abstract|sealed|async|new|partial|extern)\s+)*"
    r"(?:[\w<>,\[\]\.\?]+\s+)+([A-Za-z_]\w*)\s*\("
)


def read_text(path: Path):
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        try:
            return data.decode("utf-8-sig"), "strict"
        except UnicodeDecodeError:
            pass
    try:
        return data.decode("utf-8"), "strict"
    except UnicodeDecodeError:
        pass
    try:
        return data.decode("cp949"), "strict"
    except UnicodeDecodeError:
        return data.decode("utf-8", errors="surrogateescape"), "surrogateescape"


def context_string(path: Path) -> str:
    rel = path.relative_to(ASSETS)
    parts = list(rel.parts[:-1])
    if parts and parts[0] in {"A_Game", "B_Loby", "C_MapPreview", "D_Test", "E_ClassDiagramGenerator"}:
        parts = parts[1:]
    return " / ".join(parts) or "Root"


def describe_file(path: Path) -> str:
    stem = path.stem
    parts = stem.split(".")
    base = parts[0]
    suffixes = parts[1:]
    if stem in DIRECT:
        return DIRECT[stem]
    if suffixes and suffixes[-1] in SUFFIX:
        return f"{base}의 {SUFFIX[suffixes[-1]]}"
    if base in DIRECT:
        return DIRECT[base]
    if base.startswith("I") and len(base) > 1 and base[1].isupper():
        return "공용 인터페이스를 정의하는 파일"
    if base.endswith("Manager"):
        return "매니저 역할을 담당하는 파일"
    if base.endswith("Service"):
        return "서비스 로직을 담당하는 파일"
    if base.endswith("Library"):
        return "정의와 조회를 담당하는 라이브러리 파일"
    if base.endswith("Module"):
        return "UI 모듈 동작을 담당하는 파일"
    return "관련 구현을 담는 파일"


def method_comment(name: str) -> str:
    if name in {"Awake", "OnEnable", "Start"}:
        return "컴포넌트 참조와 초기 상태를 준비한다."
    if name == "Update":
        return "매 프레임 상태를 갱신한다."
    if name == "FixedUpdate":
        return "물리 틱 기준으로 시뮬레이션을 진행한다."
    if name == "LateUpdate":
        return "다른 업데이트 이후 시각 상태를 정리한다."
    if name in {"OnDisable", "OnDestroy", "OnApplicationQuit"}:
        return "구독과 임시 상태를 정리한다."
    if name.startswith("OnTrigger") or name.startswith("OnCollision"):
        return "충돌 또는 트리거 입력을 받아 현재 상태에 반영한다."
    if name == "OnDrawGizmosSelected":
        return "에디터에서 디버그 기즈모를 그린다."
    if name == "ToSaveData":
        return "현재 상태를 저장용 데이터로 변환한다."
    if name == "FromSaveData":
        return "저장된 데이터를 현재 인스턴스에 복원한다."
    if name.startswith("Try"):
        return "가능한 경우 작업을 시도하고 성공 여부를 반환한다."
    if name.startswith("Get"):
        return "현재 상태나 데이터를 조회한다."
    if name.startswith("Set"):
        return "값이나 연결 대상을 설정한다."
    if name.startswith("Apply"):
        return "계산 결과나 상태 변화를 현재 객체에 반영한다."
    if name.startswith("Load"):
        return "외부 데이터나 저장 내용을 읽어온다."
    if name.startswith("Save"):
        return "현재 상태를 저장 매체나 데이터 객체에 기록한다."
    if name.startswith("Create"):
        return "새 객체나 결과 데이터를 생성한다."
    if name.startswith("Build"):
        return "필요한 구조나 결과물을 구성한다."
    if name.startswith("Rebuild"):
        return "현재 조건을 기준으로 구성을 다시 만든다."
    if name.startswith("Refresh"):
        return "표시값이나 캐시를 현재 상태에 맞춰 갱신한다."
    if name.startswith("Pull"):
        return "원본 상태를 읽어와 현재 화면이나 캐시에 반영한다."
    if name.startswith("Push"):
        return "현재 변경 내용을 원본 상태에 반영한다."
    if name.startswith("Capture") or name.startswith("Snapshot"):
        return "비교나 복원을 위한 스냅샷을 기록한다."
    if name.startswith("Resolve"):
        return "필요한 대상이나 값을 해석해서 결정한다."
    if name.startswith("Place"):
        return "지정 위치나 슬롯에 대상을 배치한다."
    if name.startswith("Break") or name.startswith("Remove") or name.startswith("Clear"):
        return "대상을 제거하거나 비운다."
    if name.startswith("Step"):
        return "시뮬레이션을 한 단계 진행한다."
    if name.startswith("Process"):
        return "대기 중인 상태나 입력을 처리한다."
    if name.startswith("Calc"):
        return "필요한 값을 계산한다."
    if name.startswith("Has") or name.startswith("Is"):
        return "조건 충족 여부를 판정한다."
    if name.startswith("OnClick"):
        return "버튼 클릭 입력을 처리한다."
    if name.startswith("Bind"):
        return "필요한 참조나 이벤트를 연결한다."
    if name.startswith("Unbind"):
        return "연결된 참조나 이벤트를 해제한다."
    if name.startswith("Init"):
        return "생성 직후 필요한 데이터를 주입하고 초기화한다."
    return "이 파일의 핵심 동작을 수행한다."


def should_skip_method(line: str) -> bool:
    stripped = line.strip()
    if not stripped or stripped.startswith(CONTROL_PREFIXES):
        return True
    if stripped.startswith("#"):
        return True
    return False


def remove_existing_comment_lines(lines):
    result = []
    for line in lines:
        if line.lstrip().startswith("//"):
            continue
        result.append(line)
    return result


changed = 0
for path in FILES:
    text, write_errors = read_text(path)
    body = text.lstrip("\ufeff")
    lines = body.splitlines(True)

    while lines and lines[0].strip() == "":
        lines.pop(0)

    lines = remove_existing_comment_lines(lines)

    new_lines = []
    header = f"// [{context_string(path)}] {path.stem}: {describe_file(path)}.\r\n\r\n"
    new_lines.append(header)

    field_comment_added = False
    disabled_false_depth = 0

    for line in lines:
        stripped = line.strip()

        if stripped.startswith("#if false"):
            disabled_false_depth += 1
            new_lines.append(line)
            continue
        if stripped.startswith("#endif"):
            new_lines.append(line)
            if disabled_false_depth > 0:
                disabled_false_depth -= 1
            continue

        if disabled_false_depth == 0:
            if not field_comment_added:
                if (
                    stripped
                    and not stripped.startswith(
                        (
                            "using ",
                            "namespace ",
                            "{",
                            "}",
                            "[",
                            "public class",
                            "internal class",
                            "private class",
                            "protected class",
                            "class ",
                            "public interface",
                            "interface ",
                            "public enum",
                            "enum ",
                            "public struct",
                            "struct ",
                        )
                    )
                    and ";" in stripped
                    and "(" not in stripped
                ):
                    new_lines.append("// 인스펙터 참조와 런타임 상태 필드다.\r\n")
                    field_comment_added = True

            if not should_skip_method(line):
                match = METHOD_RE.match(line)
                if match:
                    name = match.group(1)
                    prev = new_lines[-1].strip() if new_lines else ""
                    if not prev.startswith("//"):
                        indent = re.match(r"^\s*", line).group(0)
                        new_lines.append(f"{indent}// {method_comment(name)}\r\n")

        new_lines.append(line)

    new_text = "".join(new_lines)
    if new_text != body:
        path.write_text(new_text, encoding="utf-8-sig", errors=write_errors, newline="")
        changed += 1

print(f"changed={changed}")
