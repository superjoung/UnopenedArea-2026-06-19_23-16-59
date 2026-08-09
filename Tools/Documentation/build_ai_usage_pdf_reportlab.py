from __future__ import annotations

import html
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    KeepTogether,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "AI_활용_기술문서_공모전_제출용.md"
OUTPUT = ROOT / "output" / "pdf" / "미기록구역_AI_활용_기술문서.pdf"
REGULAR_FONT = Path(r"C:\Windows\Fonts\malgun.ttf")
BOLD_FONT = Path(r"C:\Windows\Fonts\malgunbd.ttf")

PAGE_W, PAGE_H = A4
LEFT = 17 * mm
RIGHT = 17 * mm
TOP = 19 * mm
BOTTOM = 17 * mm
CONTENT_W = PAGE_W - LEFT - RIGHT

NAVY = colors.HexColor("#131F2E")
INK = colors.HexColor("#1F2A37")
MUTED = colors.HexColor("#5B6875")
TEAL = colors.HexColor("#149191")
TEAL_DARK = colors.HexColor("#0C686E")
MINT = colors.HexColor("#DEF5F1")
PAPER = colors.HexColor("#F9F8F4")
WHITE = colors.white
LINE = colors.HexColor("#D6DCE0")
SOFT = colors.HexColor("#EFF2F3")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("Malgun", str(REGULAR_FONT)))
    pdfmetrics.registerFont(TTFont("MalgunBold", str(BOLD_FONT)))
    pdfmetrics.registerFontFamily("Malgun", normal="Malgun", bold="MalgunBold")


def inline_markup(text: str) -> str:
    escaped = html.escape(text.strip())
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(r"`([^`]+)`", r'<font color="#0C686E">\1</font>', escaped)
    return escaped


class CoverFlowable(Flowable):
    def __init__(self) -> None:
        super().__init__()
        self.width = CONTENT_W - 12
        self.height = PAGE_H - TOP - BOTTOM - 12

    def draw(self) -> None:
        c = self.canv
        c.setFillColor(NAVY)
        c.rect(-LEFT, -BOTTOM, PAGE_W, PAGE_H, stroke=0, fill=1)
        c.setFillColor(TEAL)
        c.rect(-LEFT, -BOTTOM, 12, PAGE_H, stroke=0, fill=1)
        c.setFillColor(TEAL_DARK)
        c.circle(PAGE_W - LEFT - 82, PAGE_H - BOTTOM - 98, 112, stroke=0, fill=1)
        c.setStrokeColor(TEAL)
        c.setLineWidth(2)
        c.circle(PAGE_W - LEFT - 82, PAGE_H - BOTTOM - 98, 72, stroke=1, fill=0)
        c.setStrokeColor(MINT)
        c.setLineWidth(1)
        c.circle(PAGE_W - LEFT - 82, PAGE_H - BOTTOM - 98, 38, stroke=1, fill=0)

        x = 8
        c.setFillColor(TEAL)
        c.setFont("MalgunBold", 13)
        c.drawString(x, PAGE_H - 120, "AI 활용 기술 문서")
        c.setFillColor(WHITE)
        c.setFont("MalgunBold", 31)
        c.drawString(x, PAGE_H - 176, "미기록 구역")
        c.setFillColor(MINT)
        c.setFont("Malgun", 12)
        c.drawString(x + 2, PAGE_H - 212, "UNOPENED AREA")
        c.setStrokeColor(TEAL)
        c.setLineWidth(2)
        c.line(x, PAGE_H - 236, x + 260, PAGE_H - 236)

        c.setFillColor(colors.HexColor("#E3ECEF"))
        c.setFont("Malgun", 12.5)
        c.drawString(x, PAGE_H - 278, "AI 도구·프롬프트·활용 내역")
        c.drawString(x, PAGE_H - 301, "아트 제작 파이프라인 · 시스템 및 콘텐츠 개발")

        labels = [
            ("개발 환경", "Unity 6 · 2D Art · C# · ScriptableObject · Git"),
            ("AI 도구", "ChatGPT · Gemini · OpenAI Codex"),
            ("기록 기간", "2026.07.22 - 2026.08.09"),
            ("활용 원칙", "Human-in-the-loop · 결정적 게임 규칙 유지"),
        ]
        y = PAGE_H - 438
        for label, value in labels:
            c.setFillColor(TEAL)
            c.setFont("MalgunBold", 8.5)
            c.drawString(x + 2, y, label)
            c.setFillColor(WHITE)
            c.setFont("Malgun", 9.5)
            c.drawString(x + 84, y, value)
            y -= 35

        c.setFillColor(colors.HexColor("#9EADB5"))
        c.setFont("Malgun", 8.5)
        c.drawString(x, 24, "공모전 제출용 · 2026.08.09")


def draw_cover_page(canvas, doc) -> None:
    canvas.setTitle("Unopened Area - AI Usage Technical Report")
    canvas.setAuthor("Unopened Area - Art, Systems and Content Development")
    canvas.setSubject("Competition submission: AI tools, prompts, and usage history")


def draw_body_page(canvas, doc) -> None:
    canvas.saveState()
    canvas.setFillColor(PAPER)
    canvas.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    canvas.setFillColor(TEAL_DARK)
    canvas.rect(0, 0, 8, PAGE_H, stroke=0, fill=1)
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.6)
    canvas.line(LEFT, PAGE_H - 36, PAGE_W - RIGHT, PAGE_H - 36)
    canvas.line(LEFT, 34, PAGE_W - RIGHT, 34)
    canvas.setFillColor(TEAL_DARK)
    canvas.setFont("MalgunBold", 8.2)
    header = "아트 · 시스템 통합 AI 활용 기술 문서"
    canvas.drawString(LEFT, PAGE_H - 27, header)
    canvas.setFillColor(MUTED)
    canvas.setFont("Malgun", 7.2)
    canvas.drawString(LEFT, 18, "미기록 구역 · AI 활용 기술 문서")
    page_text = str(doc.page)
    canvas.drawRightString(PAGE_W - RIGHT, 18, page_text)
    canvas.restoreState()


def make_styles() -> dict[str, ParagraphStyle]:
    styles = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "TitleK", parent=styles["Heading1"], fontName="MalgunBold", fontSize=17,
            leading=23, textColor=NAVY, spaceBefore=13, spaceAfter=10,
            keepWithNext=True, wordWrap="CJK", borderColor=TEAL, borderWidth=0,
            leftIndent=10,
        ),
        "sub": ParagraphStyle(
            "SubK", parent=styles["Heading2"], fontName="MalgunBold", fontSize=11.5,
            leading=16, textColor=TEAL_DARK, spaceBefore=9, spaceAfter=5,
            keepWithNext=True, wordWrap="CJK",
        ),
        "body": ParagraphStyle(
            "BodyK", parent=styles["BodyText"], fontName="Malgun", fontSize=8.8,
            leading=14.2, textColor=INK, spaceAfter=7, wordWrap="CJK",
        ),
        "bullet": ParagraphStyle(
            "BulletK", parent=styles["BodyText"], fontName="Malgun", fontSize=8.7,
            leading=13.7, leftIndent=13, firstLineIndent=0, textColor=INK,
            bulletIndent=1, spaceAfter=3.5, wordWrap="CJK",
        ),
        "number": ParagraphStyle(
            "NumberK", parent=styles["BodyText"], fontName="Malgun", fontSize=8.7,
            leading=13.7, leftIndent=17, firstLineIndent=-15, textColor=INK,
            spaceAfter=4, wordWrap="CJK",
        ),
        "quote": ParagraphStyle(
            "QuoteK", parent=styles["BodyText"], fontName="Malgun", fontSize=8.5,
            leading=13.4, leftIndent=12, rightIndent=12, borderColor=TEAL,
            borderWidth=2, borderPadding=8, backColor=SOFT, textColor=INK,
            spaceBefore=3, spaceAfter=8, wordWrap="CJK",
        ),
        "table_header": ParagraphStyle(
            "TableHeaderK", fontName="MalgunBold", fontSize=7.1, leading=10.3,
            textColor=WHITE, wordWrap="CJK",
        ),
        "table_body": ParagraphStyle(
            "TableBodyK", fontName="Malgun", fontSize=7.0, leading=10.2,
            textColor=INK, wordWrap="CJK",
        ),
        "card_num": ParagraphStyle(
            "CardNumK", fontName="MalgunBold", fontSize=8.2, leading=11,
            textColor=TEAL,
        ),
        "card_title": ParagraphStyle(
            "CardTitleK", fontName="MalgunBold", fontSize=9.0, leading=12,
            textColor=NAVY,
        ),
        "card_body": ParagraphStyle(
            "CardBodyK", fontName="Malgun", fontSize=7.0, leading=10.1,
            textColor=MUTED, wordWrap="CJK",
        ),
        "code": ParagraphStyle(
            "CodeK", fontName="Malgun", fontSize=7.0, leading=10.5,
            textColor=colors.HexColor("#DDF5F0"), wordWrap="CJK",
        ),
    }


def section_heading(text: str, styles) -> Table:
    marker = Table([[""]], colWidths=[5], rowHeights=[22])
    marker.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), TEAL)]))
    title = Paragraph(inline_markup(text), styles["title"])
    table = Table([[marker, title]], colWidths=[7, CONTENT_W - 7], hAlign="LEFT")
    table.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"), ("LEFTPADDING", (0, 0), (-1, -1), 0), ("RIGHTPADDING", (0, 0), (-1, -1), 0)]))
    table.keepWithNext = True
    return table


def make_table(rows: list[list[str]], styles) -> Table:
    cols = max(len(row) for row in rows)
    if cols == 3:
        widths = [CONTENT_W * 0.20, CONTENT_W * 0.36, CONTENT_W * 0.44]
    elif cols == 4:
        widths = [CONTENT_W * 0.17, CONTENT_W * 0.29, CONTENT_W * 0.27, CONTENT_W * 0.27]
    else:
        widths = [CONTENT_W / cols] * cols
    data = []
    for r_index, row in enumerate(rows):
        style = styles["table_header"] if r_index == 0 else styles["table_body"]
        data.append([Paragraph(inline_markup(cell), style) for cell in row])
    table = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), TEAL_DARK),
        ("BACKGROUND", (0, 1), (-1, -1), WHITE),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [WHITE, SOFT]),
        ("GRID", (0, 0), (-1, -1), 0.45, LINE),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    return table


def make_executive_summary(styles) -> list:
    story = [section_heading("한눈에 보는 AI 활용", styles)]
    story.append(Paragraph(
        "본 프로젝트는 생성형 AI를 아트 시안·제작 공정·에셋 초안과 Unity 개발 협업에 활용했다. 이미지와 코드 결과는 담당자가 리터칭·검토·테스트한 뒤 반영했으며, 게임 규칙은 재현 가능한 데이터 로직으로 유지했다.",
        styles["body"],
    ))
    cards = [
        ("01", "비주얼 탐색", "공포 배경 시안과 키 비주얼을 빠르게 비교해 방향 확정"),
        ("02", "공정 설계", "제약 기반 심리스 타일·공간 변주·제작 우선순위 수립"),
        ("03", "에셋 제작", "컨셉아트의 배경·프랍 분리와 수작업 리터칭·색 보정"),
        ("04", "게임 적용", "Unity 프리팹·스프라이트·Animator·이상현상 데이터 연결"),
        ("05", "개발·QA", "CCTV 흐름 구현과 Definition·프리팹 ID·렌더 문제 검증"),
        ("06", "책임 있는 활용", "AI 초안과 사람의 선택·후가공·플레이 검증을 명확히 분리"),
    ]
    cells = []
    for num, title, body in cards:
        inner = Table([
            [Paragraph(num, styles["card_num"]), Paragraph(title, styles["card_title"])],
            ["", Paragraph(body, styles["card_body"])],
        ], colWidths=[24, CONTENT_W / 2 - 48])
        inner.setStyle(TableStyle([
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("SPAN", (1, 0), (1, 0)),
            ("LEFTPADDING", (0, 0), (-1, -1), 0),
            ("RIGHTPADDING", (0, 0), (-1, -1), 0),
            ("TOPPADDING", (0, 0), (-1, -1), 0),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
        ]))
        cell = Table([[inner]], colWidths=[CONTENT_W / 2 - 8], rowHeights=[56])
        cell.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), WHITE),
            ("BOX", (0, 0), (-1, -1), 0.6, LINE),
            ("LEFTPADDING", (0, 0), (-1, -1), 10),
            ("RIGHTPADDING", (0, 0), (-1, -1), 10),
            ("TOPPADDING", (0, 0), (-1, -1), 8),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ]))
        cells.append(cell)
    grid = Table([[cells[0], cells[1]], [cells[2], cells[3]], [cells[4], cells[5]]], colWidths=[CONTENT_W / 2, CONTENT_W / 2], hAlign="LEFT")
    grid.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"), ("LEFTPADDING", (0, 0), (-1, -1), 4), ("RIGHTPADDING", (0, 0), (-1, -1), 4), ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4)]))
    story.extend([grid, Spacer(1, 8), Paragraph("책임 분리", styles["sub"])])
    for item in [
        "AI: 이미지 시안, 공정 가이드, 조사·구조화, 구현 초안, 정적 검증과 문서화",
        "아티스트·개발자: 최종 방향 선택, 분리·리터칭, 수치 확정, Unity 연결과 플레이 검증",
        "게임: 정답·실패 조건은 생성형 출력이 아닌 결정적 데이터로 판정",
    ]:
        story.append(Paragraph(inline_markup(item), styles["bullet"], bulletText="-"))
    return story


def markdown_to_story(markdown: str, styles) -> list:
    story = []
    lines = markdown.splitlines()
    i = 0
    paragraph_lines: list[str] = []
    in_code = False
    code_lines: list[str] = []

    def flush_paragraph() -> None:
        nonlocal paragraph_lines
        if paragraph_lines:
            story.append(Paragraph(inline_markup(" ".join(paragraph_lines)), styles["body"]))
            paragraph_lines = []

    while i < len(lines):
        stripped = lines[i].strip()
        if stripped.startswith("```"):
            flush_paragraph()
            if in_code:
                code_html = "<br/>".join(html.escape(line or " ") for line in code_lines)
                box = Table([[Paragraph(code_html, styles["code"]) ]], colWidths=[CONTENT_W])
                box.setStyle(TableStyle([
                    ("BACKGROUND", (0, 0), (-1, -1), NAVY),
                    ("BOX", (0, 0), (-1, -1), 0.5, NAVY),
                    ("LEFTPADDING", (0, 0), (-1, -1), 12),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 12),
                    ("TOPPADDING", (0, 0), (-1, -1), 10),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
                ]))
                story.append(box)
                code_lines = []
                in_code = False
            else:
                in_code = True
            i += 1
            continue
        if in_code:
            code_lines.append(lines[i])
            i += 1
            continue
        if stripped.startswith("## "):
            flush_paragraph()
            title = stripped[3:]
            story.append(section_heading(title, styles))
            i += 1
            continue
        if stripped.startswith("### "):
            flush_paragraph()
            story.append(Paragraph(inline_markup(stripped[4:]), styles["sub"]))
            i += 1
            continue
        if stripped.startswith("| "):
            flush_paragraph()
            table_lines = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i].strip())
                i += 1
            rows = []
            for line in table_lines:
                cells = [cell.strip() for cell in line.strip("|").split("|")]
                if all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells):
                    continue
                rows.append(cells)
            story.extend([make_table(rows, styles), Spacer(1, 7)])
            continue
        number_match = re.match(r"^(\d+)\.\s+(.+)$", stripped)
        if number_match:
            flush_paragraph()
            story.append(Paragraph(f"<b>{number_match.group(1)}.</b> {inline_markup(number_match.group(2))}", styles["number"]))
            i += 1
            continue
        if stripped.startswith("- "):
            flush_paragraph()
            story.append(Paragraph(inline_markup(stripped[2:]), styles["bullet"], bulletText="-"))
            i += 1
            continue
        if stripped.startswith("> "):
            flush_paragraph()
            quote_lines = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quote_lines.append(lines[i].strip()[1:].strip())
                i += 1
            story.append(Paragraph(inline_markup(" ".join(quote_lines)), styles["quote"]))
            continue
        if not stripped:
            flush_paragraph()
        elif not stripped.startswith("# "):
            paragraph_lines.append(stripped)
        i += 1
    flush_paragraph()
    return story


def main() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    source = SOURCE.read_text(encoding="utf-8")
    start = source.find("## 1.")
    if start < 0:
        raise ValueError("Section 1 not found")

    doc = BaseDocTemplate(
        str(OUTPUT), pagesize=A4, leftMargin=LEFT, rightMargin=RIGHT,
        topMargin=TOP, bottomMargin=BOTTOM,
        title="Unopened Area - AI Usage Technical Report",
        author="Unopened Area - Art, Systems and Content Development",
        subject="Competition submission: AI tools, prompts, and usage history",
    )
    cover_frame = Frame(LEFT, BOTTOM, CONTENT_W, PAGE_H - TOP - BOTTOM, id="cover_frame", showBoundary=0)
    body_frame = Frame(LEFT, BOTTOM + 24, CONTENT_W, PAGE_H - TOP - BOTTOM - 20, id="body_frame", showBoundary=0)
    doc.addPageTemplates([
        PageTemplate(id="cover", frames=[cover_frame], onPage=draw_cover_page),
        PageTemplate(id="body", frames=[body_frame], onPage=draw_body_page),
    ])

    styles = make_styles()
    story = [CoverFlowable(), NextPageTemplate("body"), PageBreak()]
    story.extend(make_executive_summary(styles))
    story.extend(markdown_to_story(source[start:], styles))
    doc.build(story)
    print(f"created={OUTPUT}")


if __name__ == "__main__":
    main()
