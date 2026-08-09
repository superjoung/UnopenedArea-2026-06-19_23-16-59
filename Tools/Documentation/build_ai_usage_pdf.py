from __future__ import annotations

import re
from pathlib import Path

import fitz


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "AI_활용_기술문서_공모전_제출용.md"
OUTPUT = ROOT / "output" / "pdf" / "미기록구역_AI_활용_기술문서.pdf"
REGULAR_FONT = Path(r"C:\Windows\Fonts\malgun.ttf")
BOLD_FONT = Path(r"C:\Windows\Fonts\malgunbd.ttf")

PAGE_W, PAGE_H = fitz.paper_size("a4")
MARGIN_X = 46
TOP = 56
BOTTOM = 52
CONTENT_W = PAGE_W - MARGIN_X * 2

NAVY = (19 / 255, 31 / 255, 46 / 255)
INK = (31 / 255, 42 / 255, 55 / 255)
MUTED = (91 / 255, 104 / 255, 117 / 255)
TEAL = (20 / 255, 145 / 255, 145 / 255)
TEAL_DARK = (12 / 255, 104 / 255, 110 / 255)
MINT = (222 / 255, 245 / 255, 241 / 255)
PAPER = (249 / 255, 248 / 255, 244 / 255)
WHITE = (1, 1, 1)
LINE = (214 / 255, 220 / 255, 224 / 255)
SOFT = (239 / 255, 242 / 255, 243 / 255)
AMBER = (227 / 255, 165 / 255, 58 / 255)


class PdfBuilder:
    def __init__(self) -> None:
        self.doc = fitz.open()
        self.page: fitz.Page | None = None
        self.y = TOP
        self.regular = fitz.Font(fontfile=str(REGULAR_FONT))
        self.bold = fitz.Font(fontfile=str(BOLD_FONT))
        self.new_page()

    def _register_fonts(self) -> None:
        assert self.page is not None
        self.page.insert_font(fontname="Malgun", fontfile=str(REGULAR_FONT))
        self.page.insert_font(fontname="MalgunBold", fontfile=str(BOLD_FONT))

    def new_page(self, section: str | None = None) -> None:
        self.page = self.doc.new_page(width=PAGE_W, height=PAGE_H)
        self._register_fonts()
        self.page.draw_rect(fitz.Rect(0, 0, PAGE_W, PAGE_H), color=PAPER, fill=PAPER)
        self.page.draw_rect(fitz.Rect(0, 0, 8, PAGE_H), color=TEAL_DARK, fill=TEAL_DARK)
        if section:
            self.page.insert_text(
                (MARGIN_X, 28), section, fontname="MalgunBold", fontsize=8.5, color=TEAL_DARK
            )
            self.page.draw_line(
                fitz.Point(MARGIN_X, 36), fitz.Point(PAGE_W - MARGIN_X, 36), color=LINE, width=0.8
            )
        self.y = TOP

    def ensure(self, needed: float, section: str | None = None) -> None:
        if self.y + needed > PAGE_H - BOTTOM:
            self.new_page(section)

    def text_width(self, text: str, size: float, bold: bool = False) -> float:
        return (self.bold if bold else self.regular).text_length(text, fontsize=size)

    def wrap(self, text: str, width: float, size: float, bold: bool = False) -> list[str]:
        text = text.strip()
        if not text:
            return [""]
        result: list[str] = []
        for paragraph in text.splitlines():
            if not paragraph:
                result.append("")
                continue
            line = ""
            for ch in paragraph:
                candidate = line + ch
                if line and self.text_width(candidate, size, bold) > width:
                    result.append(line.rstrip())
                    line = ch.lstrip() if ch == " " else ch
                else:
                    line = candidate
            if line:
                result.append(line.rstrip())
        return result

    def write_lines(
        self,
        lines: list[str],
        x: float,
        y: float,
        size: float,
        color=INK,
        bold: bool = False,
        leading: float | None = None,
    ) -> float:
        assert self.page is not None
        leading = leading or size * 1.55
        fontname = "MalgunBold" if bold else "Malgun"
        baseline = y + size
        for line in lines:
            self.page.insert_text((x, baseline), line, fontname=fontname, fontsize=size, color=color)
            baseline += leading
        return baseline - y

    def paragraph(self, text: str, size: float = 9.2, color=INK, gap: float = 9) -> None:
        lines = self.wrap(text, CONTENT_W, size)
        height = len(lines) * size * 1.55
        self.ensure(height + gap, "AI 활용 기술 문서")
        self.y += self.write_lines(lines, MARGIN_X, self.y, size, color=color)
        self.y += gap

    def heading(self, text: str, level: int) -> None:
        if level == 2:
            if text.startswith("9.") and self.y > 430:
                self.new_page("팀원 공용 프롬프트")
            size, pre, post = 17, 16, 11
            lines = self.wrap(text, CONTENT_W, size, True)
            height = len(lines) * size * 1.35 + pre + post
            self.ensure(height + 82, "AI 활용 기술 문서")
            self.y += pre
            assert self.page is not None
            self.page.draw_rect(
                fitz.Rect(MARGIN_X, self.y + 2, MARGIN_X + 5, self.y + len(lines) * size * 1.35),
                color=TEAL,
                fill=TEAL,
            )
            self.write_lines(lines, MARGIN_X + 14, self.y, size, color=NAVY, bold=True, leading=size * 1.35)
            self.y += len(lines) * size * 1.35 + post
        else:
            size, pre, post = 12.2, 11, 6
            lines = self.wrap(text, CONTENT_W, size, True)
            height = len(lines) * size * 1.4 + pre + post
            self.ensure(height + 58, "AI 활용 기술 문서")
            self.y += pre
            self.write_lines(lines, MARGIN_X, self.y, size, color=TEAL_DARK, bold=True)
            self.y += len(lines) * size * 1.4 + post

    def bullet(self, text: str) -> None:
        width = CONTENT_W - 18
        lines = self.wrap(text, width, 9.1)
        height = len(lines) * 14.2 + 5
        self.ensure(height, "AI 활용 기술 문서")
        assert self.page is not None
        self.page.draw_circle(fitz.Point(MARGIN_X + 4, self.y + 7), 2.2, color=TEAL, fill=TEAL)
        self.write_lines(lines, MARGIN_X + 16, self.y, 9.1, leading=14.2)
        self.y += height

    def numbered(self, number: str, text: str) -> None:
        width = CONTENT_W - 30
        lines = self.wrap(text, width, 9.1)
        height = len(lines) * 14.2 + 6
        self.ensure(height, "AI 활용 기술 문서")
        assert self.page is not None
        self.page.draw_circle(fitz.Point(MARGIN_X + 10, self.y + 8), 9, color=TEAL_DARK, fill=TEAL_DARK)
        num_w = self.text_width(number, 7.6, True)
        self.page.insert_text(
            (MARGIN_X + 10 - num_w / 2, self.y + 10.5),
            number,
            fontname="MalgunBold",
            fontsize=7.6,
            color=WHITE,
        )
        self.write_lines(lines, MARGIN_X + 28, self.y, 9.1, leading=14.2)
        self.y += height

    def quote(self, text: str) -> None:
        lines = self.wrap(text, CONTENT_W - 32, 8.8)
        height = len(lines) * 13.8 + 22
        self.ensure(height + 8, "AI 활용 기술 문서")
        assert self.page is not None
        rect = fitz.Rect(MARGIN_X, self.y, PAGE_W - MARGIN_X, self.y + height)
        self.page.draw_rect(rect, color=LINE, fill=SOFT, radius=0.08)
        self.page.draw_rect(
            fitz.Rect(MARGIN_X, self.y, MARGIN_X + 5, self.y + height), color=TEAL, fill=TEAL
        )
        self.write_lines(lines, MARGIN_X + 18, self.y + 9, 8.8, color=INK, leading=13.8)
        self.y += height + 8

    def code_block(self, text: str) -> None:
        lines: list[str] = []
        for raw in text.splitlines():
            lines.extend(self.wrap(raw or " ", CONTENT_W - 30, 7.5))
        max_lines = 42
        chunks = [lines[i : i + max_lines] for i in range(0, len(lines), max_lines)]
        for chunk in chunks:
            height = len(chunk) * 11.2 + 24
            self.ensure(height + 10, "팀원 공용 프롬프트")
            assert self.page is not None
            rect = fitz.Rect(MARGIN_X, self.y, PAGE_W - MARGIN_X, self.y + height)
            self.page.draw_rect(rect, color=NAVY, fill=NAVY, radius=0.04)
            self.write_lines(chunk, MARGIN_X + 15, self.y + 10, 7.5, color=(0.86, 0.96, 0.94), leading=11.2)
            self.y += height + 10

    def table(self, rows: list[list[str]]) -> None:
        if not rows:
            return
        cols = max(len(r) for r in rows)
        weights = [1.0] * cols
        if cols == 3:
            weights = [0.8, 1.45, 1.75]
        total = sum(weights)
        widths = [CONTENT_W * w / total for w in weights]
        prepared: list[tuple[list[list[str]], float]] = []
        for row_index, row in enumerate(rows):
            padded = row + [""] * (cols - len(row))
            line_sets = [self.wrap(cell, widths[i] - 14, 7.7, row_index == 0) for i, cell in enumerate(padded)]
            height = max(len(lines) for lines in line_sets) * 11.5 + 16
            prepared.append((line_sets, height))

        total_height = sum(height for _, height in prepared) + 10
        if total_height <= PAGE_H - TOP - BOTTOM and self.y + total_height > PAGE_H - BOTTOM:
            self.new_page("AI 활용 기술 문서")

        for row_index, (line_sets, height) in enumerate(prepared):
            self.ensure(height + (5 if row_index == 0 else 0), "AI 활용 기술 문서")
            assert self.page is not None
            fill = TEAL_DARK if row_index == 0 else (WHITE if row_index % 2 else SOFT)
            text_color = WHITE if row_index == 0 else INK
            x = MARGIN_X
            for i, lines in enumerate(line_sets):
                rect = fitz.Rect(x, self.y, x + widths[i], self.y + height)
                self.page.draw_rect(rect, color=LINE if row_index else TEAL_DARK, fill=fill, width=0.6)
                self.write_lines(
                    lines,
                    x + 7,
                    self.y + 6,
                    7.7,
                    color=text_color,
                    bold=row_index == 0,
                    leading=11.5,
                )
                x += widths[i]
            self.y += height
        self.y += 10

    def cover(self) -> None:
        assert self.page is not None
        self.page.draw_rect(fitz.Rect(0, 0, PAGE_W, PAGE_H), color=NAVY, fill=NAVY)
        self.page.draw_rect(fitz.Rect(0, 0, 12, PAGE_H), color=TEAL, fill=TEAL)
        self.page.draw_circle(fitz.Point(PAGE_W - 80, 95), 112, color=TEAL_DARK, fill=TEAL_DARK)
        self.page.draw_circle(fitz.Point(PAGE_W - 80, 95), 72, color=TEAL, width=2)
        self.page.draw_circle(fitz.Point(PAGE_W - 80, 95), 38, color=MINT, width=1.2)

        self.page.insert_text((55, 120), "AI 활용 기술 문서", fontname="MalgunBold", fontsize=13, color=TEAL)
        title_lines = ["미기록 구역", "UNOPENED AREA"]
        self.page.insert_text((55, 172), title_lines[0], fontname="MalgunBold", fontsize=31, color=WHITE)
        self.page.insert_text((57, 210), title_lines[1], fontname="Malgun", fontsize=12, color=MINT)
        self.page.draw_line(fitz.Point(55, 235), fitz.Point(315, 235), color=TEAL, width=2)

        desc = "AI 도구·프롬프트·활용 내역\n시스템 및 콘텐츠 개발 파트"
        self.write_lines(desc.splitlines(), 55, 265, 12.5, color=(0.89, 0.93, 0.94), leading=22)

        labels = [
            ("개발 환경", "Unity 6 · C# · ScriptableObject · Git"),
            ("AI 도구", "OpenAI Codex 코딩 에이전트"),
            ("기록 기간", "2026.07.22 - 2026.08.09"),
            ("활용 원칙", "Human-in-the-loop · 결정적 게임 규칙 유지"),
        ]
        y = 420
        for label, value in labels:
            self.page.insert_text((57, y), label, fontname="MalgunBold", fontsize=8.5, color=TEAL)
            self.page.insert_text((140, y), value, fontname="Malgun", fontsize=9.5, color=WHITE)
            y += 35

        self.page.insert_text(
            (55, PAGE_H - 62),
            "공모전 제출용 · 2026.08.09",
            fontname="Malgun",
            fontsize=8.5,
            color=(0.65, 0.72, 0.76),
        )

    def executive_summary(self) -> None:
        self.new_page("EXECUTIVE SUMMARY")
        self.heading("한눈에 보는 AI 활용", 2)
        self.paragraph(
            "본 프로젝트는 생성형 AI를 게임 판정에 직접 사용하지 않고, 복잡한 Unity 프로젝트를 분석하고 구현·검증하는 개발 협업 에이전트로 활용했다. AI 제안은 개발자가 검토한 뒤 반영했으며, 게임 규칙은 재현 가능한 데이터 로직으로 유지했다."
        )
        cards = [
            ("01", "요구사항 통합", "PRD·시나리오·콘텐츠표의 충돌과 구현 우선순위 정리"),
            ("02", "시스템 설계", "CCTV·보고·미보고·정전·현장 이동 상태 흐름 설계"),
            ("03", "코드 구현", "C# 컴포넌트와 데이터 기반 콘텐츠 파이프라인 구현"),
            ("04", "데이터 QA", "Definition·프리팹 ID·보고 유형·활성 상태 교차 검사"),
            ("05", "디버깅", "스크린샷·로그·코드로 전환·입력·렌더 문제 원인 추적"),
            ("06", "기록·인수인계", "날짜별 활용 내역과 Git 근거, 팀원용 재사용 프롬프트 작성"),
        ]
        assert self.page is not None
        card_w = (CONTENT_W - 14) / 2
        card_h = 84
        start_y = self.y + 4
        for i, (num, title, desc) in enumerate(cards):
            col, row = i % 2, i // 2
            x = MARGIN_X + col * (card_w + 14)
            y = start_y + row * (card_h + 12)
            rect = fitz.Rect(x, y, x + card_w, y + card_h)
            self.page.draw_rect(rect, color=LINE, fill=WHITE, radius=0.08)
            self.page.insert_text((x + 13, y + 23), num, fontname="MalgunBold", fontsize=10, color=TEAL)
            self.page.insert_text((x + 45, y + 23), title, fontname="MalgunBold", fontsize=10.5, color=NAVY)
            lines = self.wrap(desc, card_w - 26, 7.8)
            self.write_lines(lines, x + 13, y + 36, 7.8, color=MUTED, leading=11.7)
        self.y = start_y + 3 * (card_h + 12) + 8
        self.heading("책임 분리", 3)
        self.bullet("AI: 조사, 구조화, 구현 초안, 반복 수정, 정적 검증과 문서화")
        self.bullet("개발자: 목표·연출·수치 확정, 아트·사운드 배치, Inspector 연결과 플레이 검증")
        self.bullet("게임: 정답·실패 조건은 생성형 출력이 아닌 결정적 데이터로 판정")

    def add_footers(self) -> None:
        total = len(self.doc)
        for index, page in enumerate(self.doc):
            if index == 0:
                continue
            page.insert_font(fontname="Malgun", fontfile=str(REGULAR_FONT))
            page.draw_line(
                fitz.Point(MARGIN_X, PAGE_H - 34), fitz.Point(PAGE_W - MARGIN_X, PAGE_H - 34), color=LINE, width=0.6
            )
            page.insert_text(
                (MARGIN_X, PAGE_H - 19),
                "미기록 구역 · AI 활용 기술 문서",
                fontname="Malgun",
                fontsize=7.2,
                color=MUTED,
            )
            page_num = f"{index + 1} / {total}"
            width = self.regular.text_length(page_num, fontsize=7.2)
            page.insert_text(
                (PAGE_W - MARGIN_X - width, PAGE_H - 19),
                page_num,
                fontname="Malgun",
                fontsize=7.2,
                color=MUTED,
            )

    def save(self) -> None:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        self.add_footers()
        self.doc.set_metadata(
            {
                "title": "Unopened Area - AI Usage Technical Report",
                "author": "Unopened Area - Systems and Content Development",
                "subject": "Competition submission: AI tools, prompts, and usage history",
                "keywords": "AI, Codex, Unity, game development, competition",
            }
        )
        self.doc.save(OUTPUT, garbage=4, deflate=True, clean=True)


def clean_inline(text: str) -> str:
    text = re.sub(r"`([^`]+)`", r"\1", text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"\1", text)
    return text.strip()


def render_markdown(builder: PdfBuilder, markdown: str) -> None:
    lines = markdown.splitlines()
    i = 0
    in_code = False
    code_lines: list[str] = []
    paragraph_lines: list[str] = []

    def flush_paragraph() -> None:
        nonlocal paragraph_lines
        if paragraph_lines:
            builder.paragraph(clean_inline(" ".join(line.strip() for line in paragraph_lines)))
            paragraph_lines = []

    while i < len(lines):
        raw = lines[i]
        stripped = raw.strip()

        if stripped.startswith("```"):
            flush_paragraph()
            if in_code:
                builder.code_block("\n".join(code_lines))
                code_lines = []
                in_code = False
            else:
                in_code = True
            i += 1
            continue
        if in_code:
            code_lines.append(raw)
            i += 1
            continue

        if stripped.startswith("# "):
            flush_paragraph()
            i += 1
            continue
        if stripped.startswith("## "):
            flush_paragraph()
            builder.heading(clean_inline(stripped[3:]), 2)
            i += 1
            continue
        if stripped.startswith("### "):
            flush_paragraph()
            builder.heading(clean_inline(stripped[4:]), 3)
            i += 1
            continue
        if stripped.startswith("| "):
            flush_paragraph()
            table_lines: list[str] = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i].strip())
                i += 1
            rows: list[list[str]] = []
            for table_line in table_lines:
                cells = [clean_inline(cell) for cell in table_line.strip("|").split("|")]
                if all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells):
                    continue
                rows.append(cells)
            builder.table(rows)
            continue
        if re.match(r"^\d+\.\s+", stripped):
            flush_paragraph()
            match = re.match(r"^(\d+)\.\s+(.+)$", stripped)
            assert match
            builder.numbered(match.group(1), clean_inline(match.group(2)))
            i += 1
            continue
        if stripped.startswith("- "):
            flush_paragraph()
            builder.bullet(clean_inline(stripped[2:]))
            i += 1
            continue
        if stripped.startswith("> "):
            flush_paragraph()
            quote_lines: list[str] = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quote_lines.append(lines[i].strip()[1:].strip())
                i += 1
            builder.quote(clean_inline(" ".join(quote_lines)))
            continue
        if not stripped:
            flush_paragraph()
        else:
            paragraph_lines.append(stripped)
        i += 1

    flush_paragraph()


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not REGULAR_FONT.exists() or not BOLD_FONT.exists():
        raise FileNotFoundError("Malgun Gothic fonts are required")

    source = SOURCE.read_text(encoding="utf-8")
    builder = PdfBuilder()
    builder.cover()
    builder.executive_summary()

    start = source.find("## 1.")
    if start < 0:
        raise ValueError("Source document does not contain section 1")
    render_markdown(builder, source[start:])
    builder.save()
    print(f"created={OUTPUT}")
    print(f"pages={len(builder.doc)}")


if __name__ == "__main__":
    # Keep the original entry point for compatibility, but use the ReportLab
    # renderer so Korean remains searchable and copyable in the final PDF.
    from build_ai_usage_pdf_reportlab import main as reportlab_main

    reportlab_main()
