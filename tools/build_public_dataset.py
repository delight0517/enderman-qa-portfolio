from __future__ import annotations
import argparse, hashlib, json, re, zipfile
from datetime import datetime, timezone
from pathlib import Path
from xml.etree import ElementTree as ET

NS = {
    "m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
}


def read_test_cases(path: Path) -> list[dict[str, str]]:
    with zipfile.ZipFile(path) as book:
        workbook = ET.fromstring(book.read("xl/workbook.xml"))
        rels = ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))
        relmap = {item.attrib["Id"]: item.attrib["Target"].lstrip("/") for item in rels}
        sheet_paths = {
            sheet.attrib["name"]: relmap[sheet.attrib[f"{{{NS['r']}}}id"]]
            for sheet in workbook.findall("m:sheets/m:sheet", NS)
        }
        root = ET.fromstring(book.read(sheet_paths["Test Cases"]))
        shared: list[str] = []
        if "xl/sharedStrings.xml" in book.namelist():
            ss = ET.fromstring(book.read("xl/sharedStrings.xml"))
            shared = ["".join(part.text or "" for part in si.findall(".//m:t", NS)) for si in ss.findall("m:si", NS)]

        def value(cell: ET.Element) -> str:
            if cell.attrib.get("t") == "inlineStr":
                return "".join(part.text or "" for part in cell.findall(".//m:t", NS))
            node = cell.find("m:v", NS)
            if node is None or node.text is None:
                return ""
            if cell.attrib.get("t") == "s":
                return shared[int(node.text)]
            return node.text

        rows = root.findall("m:sheetData/m:row", NS)
        if not rows:
            raise ValueError("Test Cases sheet is empty")
        headers = [value(cell) for cell in rows[0].findall("m:c", NS)]
        records = []
        for row in rows[1:]:
            cells = [value(cell) for cell in row.findall("m:c", NS)]
            if not cells or not any(cells):
                continue
            if len(cells) > len(headers):
                raise ValueError("Unexpected column count in Test Cases")
            cells += [""] * (len(headers) - len(cells))
            records.append(dict(zip(headers, cells)))
        return records


def category_for(title: str, actions: str, expected: str) -> str:
    text = f"{title} {actions} {expected}".lower()
    groups = [
        ("물·물약", r"물약|포션|물\b|익사|물속|구름|potion|water|drown|cloud"),
        ("투사체·폭발", r"화살|투사체|활|삼지창|눈덩|달걀|폭발|arrow|projectile|bow|trident|snowball|explosion"),
        ("환경 피해", r"낙하|용암|불|화염|선인장|공허|질식|추락|fall|lava|fire|cactus|void|suffocat"),
        ("이동·순간이동", r"순간이동|텔레포트|좌표|거리|차원|teleport|dimension|position"),
        ("사망·드롭", r"드롭|경험치|아이템|전리품|사망|죽음|drop|experience|loot|death"),
        ("공격·피해", r"공격|검|도끼|맨손|무기|피해|방패|attack|sword|axe|bare-hand|damage|shield|knockback"),
    ]
    for label, pattern in groups:
        if re.search(pattern, text):
            return label
    return "생존·기타"


def public_case(row: dict[str, str]) -> dict[str, object]:
    case_num = re.sub(r"\D", "", row.get("Case #", ""))
    if not case_num:
        case_num = re.sub(r"\D", "", row.get("Case ID", ""))[-3:]
    verdict = row.get("Verdict", "").strip()
    coverage = row.get("Coverage", "").strip()
    if verdict == "Pass":
        state = "기록 범위 Pass" if coverage.lower().startswith("full:") else "부분 범위 Pass"
        public_actual = "실행 기록의 통과 판정을 반영했습니다. 확인 범위와 한계를 함께 확인해 주세요."
    elif verdict == "Inconclusive":
        state = "미확정"
        public_actual = "조건 또는 증거가 충분하지 않아 게임 동작의 통과·실패를 판정하지 않았습니다."
    else:
        state = "미실행"
        public_actual = "이 케이스는 Minecraft 동작으로 실행되지 않았습니다."
    title = row.get("Title", "").strip()
    actions = row.get("Expected actions", "").strip()
    expected = row.get("Expected observation", "").strip()
    criteria = row.get("Pass criteria", "").strip()
    limitation = row.get("Limitation", "").strip()
    coverage_text = coverage.split(":", 1)[-1].strip() if ":" in coverage else coverage
    # Never export source paths, UUIDs, account identifiers, journal IDs or raw log content.
    preconditions = row.get("Preconditions", "").strip()
    fields = [title, actions, expected, criteria, limitation, coverage_text, preconditions]
    for item in fields:
        if re.search(r"(?i)([A-Z]:\\|[0-9a-f]{32,}|@|bearer\s|token\s*=)", item):
            raise ValueError("Private identifier/path detected in a public text field")
    attempts = row.get("Attempts", "").strip()
    return {
        "number": int(case_num),
        "id": f"TC-{int(case_num):03d}",
        "title": title,
        "category": category_for(title, actions, expected),
        "verdict": verdict,
        "status": state,
        "dispatched": verdict != "NotRun",
        "preconditions": preconditions,
        "expectedActions": actions,
        "expectedObservation": expected,
        "passCriteria": criteria,
        "actualSummary": public_actual,
        "coverage": coverage_text,
        "coverageLevel": "full" if state == "기록 범위 Pass" else ("none" if state == "미실행" else "partial"),
        "attempts": int(float(attempts)) if attempts else 0,
        "repeatability": {
            "confirmed-repeatable-same-signature": "같은 결과로 재현됨",
            "not-needed": "재현 테스트 불필요",
            "repeatable-inconclusive-evidence-not-a-product-reproduction": "미확정 결과가 반복됨",
            "not-confirmed-signatures-diverged-or-incomplete": "결과가 일치하지 않거나 증거가 부족함",
        }.get(row.get("Repeatability", "").strip(), "반복성 판정 자료 없음"),
        "limitation": limitation,
        "visualEvidence": False,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workbook", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    workbook_name = args.workbook.name
    date_match = re.search(r"(20\d{6})", workbook_name)
    if not date_match:
        raise ValueError("Workbook filename must include its YYYYMMDD reconciliation date")
    reconciled = datetime.strptime(date_match.group(1), "%Y%m%d").date().isoformat()
    workbook_hash = hashlib.sha256(args.workbook.read_bytes()).hexdigest()
    rows = read_test_cases(args.workbook)
    cases = [public_case(row) for row in rows]
    cases.sort(key=lambda item: int(item["number"]))
    if len(cases) != 100 or len({item["number"] for item in cases}) != 100:
        raise ValueError(f"Expected exactly 100 unique cases, got {len(cases)}")
    counts = {label: sum(1 for item in cases if item["status"] == label) for label in ("기록 범위 Pass", "부분 범위 Pass", "미확정", "미실행")}
    payload = {
        "schemaVersion": 1,
        "project": "Enderman 100 TC",
        "sourceWorkbook": workbook_name,
        "sourceWorkbookSha256": workbook_hash,
        "lastReconciled": reconciled,
        "totalCases": len(cases),
        "dispatchedAtLeastOnce": sum(1 for item in cases if item["dispatched"]),
        "counts": counts,
        "confirmedDefects": 0,
        "screenshotsOrVideo": 0,
        "cases": cases,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # Keep generated snapshots byte-stable across Windows and Unix checkouts.
    args.output.write_bytes((json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps({"totalCases": len(cases), "dispatchedAtLeastOnce": payload["dispatchedAtLeastOnce"], "sourceWorkbook": workbook_name, "sourceWorkbookSha256": workbook_hash, "lastReconciled": reconciled, "counts": counts}, ensure_ascii=True))

if __name__ == "__main__":
    main()
