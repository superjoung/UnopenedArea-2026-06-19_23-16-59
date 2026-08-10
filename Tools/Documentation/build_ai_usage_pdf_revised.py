from __future__ import annotations

import html
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas as pdfcanvas
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "AI_활용_기술문서_개정판_원고.md"
OUTPUT = ROOT / "output" / "pdf" / "미기록구역_AI_활용_기술문서_개정판.pdf"

FONT_CANDIDATES = [
    Path("/System/Library/Fonts/Supplemental/AppleGothic.ttf"),
    Path("C:/Windows/Fonts/malgun.ttf"),
]
BOLD_FONT_CANDIDATES = [
    Path("C:/Windows/Fonts/malgunbd.ttf"),
    Path("/System/Library/Fonts/Supplemental/AppleGothic.ttf"),
]

PAGE_W, PAGE_H = A4
LEFT = 17 * mm
RIGHT = 17 * mm
TOP = 19 * mm
BOTTOM = 17 * mm
CONTENT_W = PAGE_W - LEFT - RIGHT

NAVY = colors.HexColor("#162333")
INK = colors.HexColor("#23303D")
MUTED = colors.HexColor("#62707D")
PAPER = colors.HexColor("#FAF9F5")
WHITE = colors.white
LINE = colors.HexColor("#D5DCE1")
SOFT = colors.HexColor("#F0F3F4")

ROLE_COLORS = {
    "기획": colors.HexColor("#6B5CA5"),
    "아트": colors.HexColor("#B76A3C"),
    "개발": colors.HexColor("#326B9A"),
    "통합·QA": colors.HexColor("#14777B"),
    "공통": colors.HexColor("#14777B"),
}

ROLE_LIGHT = {
    "기획": colors.HexColor("#F0EDF8"),
    "아트": colors.HexColor("#F8EFE9"),
    "개발": colors.HexColor("#EAF1F7"),
    "통합·QA": colors.HexColor("#E8F4F3"),
    "공통": colors.HexColor("#E8F4F3"),
}


def first_existing(paths: list[Path]) -> Path:
    for path in paths:
        if path.exists():
            return path
    raise FileNotFoundError(f"Korean font not found: {paths}")


def register_fonts() -> None:
    regular = first_existing(FONT_CANDIDATES)
    bold = first_existing(BOLD_FONT_CANDIDATES)
    pdfmetrics.registerFont(TTFont("DocRegular", str(regular)))
    pdfmetrics.registerFont(TTFont("DocBold", str(bold)))
    pdfmetrics.registerFontFamily(
        "DocRegular",
        normal="DocRegular",
        bold="DocBold",
        italic="DocRegular",
        boldItalic="DocBold",
    )


def role_from_heading(text: str) -> str:
    match = re.match(r"^\[([^]]+)\]", text.strip())
    if not match:
        return "공통"
    role = match.group(1)
    return role if role in ROLE_COLORS else "공통"


def inline_markup(text: str) -> str:
    escaped = html.escape(text.strip())
    escaped = escaped.replace("&lt;br/&gt;", "<br/>").replace("&lt;br /&gt;", "<br/>")
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(r"`([^`]+)`", r'<font color="#14777B">\1</font>', escaped)
    return escaped


class CoverFlowable(Flowable):
    def __init__(self) -> None:
        super().__init__()
        self.width = CONTENT_W - 12
        self.height = PAGE_H - TOP - BOTTOM - 12

    def draw(self) -> None:
        canvas = self.canv
        canvas.setFillColor(NAVY)
        canvas.rect(-LEFT, -BOTTOM, PAGE_W, PAGE_H, stroke=0, fill=1)

        bands = ["기획", "아트", "개발", "통합·QA"]
        band_h = PAGE_H / len(bands)
        for index, role in enumerate(bands):
            canvas.setFillColor(ROLE_COLORS[role])
            canvas.rect(-LEFT, -BOTTOM + index * band_h, 11, band_h, stroke=0, fill=1)

        canvas.setFillColor(colors.HexColor("#20394B"))
        canvas.circle(PAGE_W - LEFT - 68, PAGE_H - BOTTOM - 96, 112, stroke=0, fill=1)
        canvas.setStrokeColor(ROLE_COLORS["통합·QA"])
        canvas.setLineWidth(2)
        canvas.circle(PAGE_W - LEFT - 68, PAGE_H - BOTTOM - 96, 72, stroke=1, fill=0)
        canvas.setStrokeColor(colors.HexColor("#B7D8D7"))
        canvas.setLineWidth(1)
        canvas.circle(PAGE_W - LEFT - 68, PAGE_H - BOTTOM - 96, 38, stroke=1, fill=0)

        x = 8
        canvas.setFillColor(ROLE_COLORS["통합·QA"])
        canvas.setFont("DocBold", 13)
        canvas.drawString(x, PAGE_H - 120, "AI 활용 기술 문서 · 개정판")
        canvas.setFillColor(WHITE)
        canvas.setFont("DocBold", 31)
        canvas.drawString(x, PAGE_H - 177, "미기록 구역")
        canvas.setFillColor(colors.HexColor("#C6E2E0"))
        canvas.setFont("DocRegular", 12)
        canvas.drawString(x + 2, PAGE_H - 212, "UNOPENED AREA")

        canvas.setStrokeColor(ROLE_COLORS["통합·QA"])
        canvas.setLineWidth(2)
        canvas.line(x, PAGE_H - 236, x + 270, PAGE_H - 236)

        canvas.setFillColor(colors.HexColor("#E3ECEF"))
        canvas.setFont("DocRegular", 12)
        canvas.drawString(x, PAGE_H - 276, "기획 · 아트 · 개발 · 통합 QA")
        canvas.drawString(x, PAGE_H - 299, "AI 도구 · 프롬프트 · 실제 적용과 검증")

        roles = [
            ("기획", "아이디어를 제작 가능한 규칙과 데이터로 수렴"),
            ("아트", "생성 시안, 공정 설계와 사람의 리터칭"),
            ("개발", "Unity 구현, 저장소 분석과 플레이 검증"),
            ("통합·QA", "MD 문맥 관리, ID 재검색과 인수인계"),
        ]
        y = PAGE_H - 426
        for role, description in roles:
            canvas.setFillColor(ROLE_COLORS[role])
            canvas.roundRect(x, y - 13, 60, 22, 4, stroke=0, fill=1)
            canvas.setFillColor(WHITE)
            canvas.setFont("DocBold", 8)
            canvas.drawCentredString(x + 30, y - 5, role)
            canvas.setFillColor(colors.HexColor("#E7EEF1"))
            canvas.setFont("DocRegular", 9.2)
            canvas.drawString(x + 74, y - 5, description)
            y -= 40

        canvas.setFillColor(colors.HexColor("#9FADB6"))
        canvas.setFont("DocRegular", 8.5)
        canvas.drawString(x, 24, "NHN 공모전 제출용 · 2026.08.10")


def draw_cover_page(canvas, doc) -> None:
    canvas.setTitle("Unopened Area - Revised AI Usage Technical Report")
    canvas.setAuthor("Unopened Area - Planning, Art and Development")
    canvas.setSubject("Competition submission: revised AI usage, prompts and collaboration process")

    canvas.setFillColor(NAVY)
    canvas.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)

    bands = ["기획", "아트", "개발", "통합·QA"]
    band_h = PAGE_H / len(bands)
    for index, role in enumerate(bands):
        canvas.setFillColor(ROLE_COLORS[role])
        canvas.rect(0, index * band_h, 11, band_h, stroke=0, fill=1)

    canvas.setFillColor(colors.HexColor("#20394B"))
    canvas.circle(PAGE_W - 68, PAGE_H - 96, 112, stroke=0, fill=1)
    canvas.setStrokeColor(ROLE_COLORS["통합·QA"])
    canvas.setLineWidth(2)
    canvas.circle(PAGE_W - 68, PAGE_H - 96, 72, stroke=1, fill=0)
    canvas.setStrokeColor(colors.HexColor("#B7D8D7"))
    canvas.setLineWidth(1)
    canvas.circle(PAGE_W - 68, PAGE_H - 96, 38, stroke=1, fill=0)

    x = 62
    canvas.setFillColor(ROLE_COLORS["통합·QA"])
    canvas.setFont("DocBold", 13)
    canvas.drawString(x, PAGE_H - 120, "AI 활용 기술 문서 · 개정판")
    canvas.setFillColor(WHITE)
    canvas.setFont("DocBold", 31)
    canvas.drawString(x, PAGE_H - 177, "미기록 구역")
    canvas.setFillColor(colors.HexColor("#C6E2E0"))
    canvas.setFont("DocRegular", 12)
    canvas.drawString(x + 2, PAGE_H - 212, "UNOPENED AREA")

    canvas.setStrokeColor(ROLE_COLORS["통합·QA"])
    canvas.setLineWidth(2)
    canvas.line(x, PAGE_H - 236, x + 270, PAGE_H - 236)

    canvas.setFillColor(colors.HexColor("#E3ECEF"))
    canvas.setFont("DocRegular", 12)
    canvas.drawString(x, PAGE_H - 276, "기획 · 아트 · 개발 · 통합 QA")
    canvas.drawString(x, PAGE_H - 299, "AI 도구 · 프롬프트 · 실제 적용과 검증")

    roles = [
        ("기획", "아이디어를 제작 가능한 규칙과 데이터로 수렴"),
        ("아트", "생성 시안, 공정 설계와 사람의 리터칭"),
        ("개발", "Unity 구현, 저장소 분석과 플레이 검증"),
        ("통합·QA", "MD 문맥 관리, ID 재검색과 인수인계"),
    ]
    y = PAGE_H - 426
    for role, description in roles:
        canvas.setFillColor(ROLE_COLORS[role])
        canvas.roundRect(x, y - 13, 60, 22, 4, stroke=0, fill=1)
        canvas.setFillColor(WHITE)
        canvas.setFont("DocBold", 8)
        canvas.drawCentredString(x + 30, y - 5, role)
        canvas.setFillColor(colors.HexColor("#E7EEF1"))
        canvas.setFont("DocRegular", 9.2)
        canvas.drawString(x + 74, y - 5, description)
        y -= 40

    canvas.setFillColor(colors.HexColor("#9FADB6"))
    canvas.setFont("DocRegular", 8.5)
    canvas.drawString(x, 72, "NHN 공모전 제출용 · 2026.08.10")


def draw_role_legend(canvas) -> None:
    x = PAGE_W - RIGHT - 230
    y = PAGE_H - 26
    for role in ["기획", "아트", "개발", "통합·QA"]:
        color = ROLE_COLORS[role]
        canvas.setFillColor(color)
        canvas.roundRect(x, y - 7, 9, 9, 2, stroke=0, fill=1)
        canvas.setFillColor(MUTED)
        canvas.setFont("DocRegular", 6.6)
        canvas.drawString(x + 13, y - 5, role)
        x += 48 if role != "통합·QA" else 58


def draw_body_page(canvas, doc) -> None:
    canvas.saveState()
    canvas.setFillColor(PAPER)
    canvas.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    canvas.setFillColor(NAVY)
    canvas.rect(0, 0, 7, PAGE_H, stroke=0, fill=1)
    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.6)
    canvas.line(LEFT, PAGE_H - 36, PAGE_W - RIGHT, PAGE_H - 36)
    canvas.line(LEFT, 34, PAGE_W - RIGHT, 34)
    canvas.setFillColor(NAVY)
    canvas.setFont("DocBold", 8.2)
    canvas.drawString(LEFT, PAGE_H - 27, "미기록 구역 · AI 활용 기술 문서 개정판")
    draw_role_legend(canvas)
    canvas.setFillColor(MUTED)
    canvas.setFont("DocRegular", 7.2)
    canvas.drawString(LEFT, 18, "기획 · 아트 · 개발 · 통합 QA")
    canvas.drawRightString(PAGE_W - RIGHT, 18, str(doc.page))
    canvas.restoreState()


def draw_document_page(canvas, doc) -> None:
    if doc.page == 1:
        draw_cover_page(canvas, doc)
    else:
        draw_body_page(canvas, doc)


def make_styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "Title",
            parent=base["Heading1"],
            fontName="DocBold",
            fontSize=16.5,
            leading=22,
            textColor=NAVY,
            spaceBefore=8,
            spaceAfter=9,
            keepWithNext=True,
            wordWrap="CJK",
        ),
        "sub": ParagraphStyle(
            "Sub",
            parent=base["Heading2"],
            fontName="DocBold",
            fontSize=11.2,
            leading=15.5,
            textColor=NAVY,
            spaceBefore=8,
            spaceAfter=5,
            keepWithNext=True,
            wordWrap="CJK",
        ),
        "body": ParagraphStyle(
            "Body",
            parent=base["BodyText"],
            fontName="DocRegular",
            fontSize=8.6,
            leading=13.6,
            textColor=INK,
            spaceAfter=6,
            wordWrap="CJK",
            allowWidows=0,
            allowOrphans=0,
        ),
        "bullet": ParagraphStyle(
            "Bullet",
            parent=base["BodyText"],
            fontName="DocRegular",
            fontSize=8.4,
            leading=13,
            leftIndent=13,
            bulletIndent=1,
            textColor=INK,
            spaceAfter=2.5,
            wordWrap="CJK",
        ),
        "number": ParagraphStyle(
            "Number",
            parent=base["BodyText"],
            fontName="DocRegular",
            fontSize=8.3,
            leading=12.7,
            leftIndent=17,
            firstLineIndent=-15,
            textColor=INK,
            spaceAfter=3,
            wordWrap="CJK",
        ),
        "quote": ParagraphStyle(
            "Quote",
            parent=base["BodyText"],
            fontName="DocRegular",
            fontSize=8.3,
            leading=13,
            leftIndent=12,
            rightIndent=12,
            borderWidth=0,
            borderPadding=8,
            backColor=SOFT,
            textColor=INK,
            spaceBefore=3,
            spaceAfter=8,
            wordWrap="CJK",
        ),
        "table_header": ParagraphStyle(
            "TableHeader",
            fontName="DocBold",
            fontSize=6.8,
            leading=9.4,
            textColor=WHITE,
            wordWrap="CJK",
        ),
        "table_body": ParagraphStyle(
            "TableBody",
            fontName="DocRegular",
            fontSize=6.7,
            leading=9.5,
            textColor=INK,
            wordWrap="CJK",
        ),
        "table_first": ParagraphStyle(
            "TableFirst",
            fontName="DocBold",
            fontSize=6.7,
            leading=9.5,
            textColor=NAVY,
            wordWrap="CJK",
        ),
    }


def section_heading(text: str, styles, role: str) -> Table:
    accent = ROLE_COLORS[role]
    label = Table([[role]], colWidths=[50], rowHeights=[20])
    label.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), accent),
                ("TEXTCOLOR", (0, 0), (-1, -1), WHITE),
                ("FONTNAME", (0, 0), (-1, -1), "DocBold"),
                ("FONTSIZE", (0, 0), (-1, -1), 7.5),
                ("ALIGN", (0, 0), (-1, -1), "CENTER"),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
            ]
        )
    )
    clean_title = re.sub(r"^\[[^]]+\]\s*", "", text)
    title = Paragraph(inline_markup(clean_title), styles["title"])
    table = Table([[label, title]], colWidths=[58, CONTENT_W - 58], hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("LEFTPADDING", (0, 0), (-1, -1), 0),
                ("RIGHTPADDING", (0, 0), (-1, -1), 0),
                ("TOPPADDING", (0, 0), (-1, -1), 0),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
            ]
        )
    )
    table.keepWithNext = True
    return table


def make_table(rows: list[list[str]], styles, role: str) -> Table:
    cols = max(len(row) for row in rows)
    if cols == 2:
        widths = [CONTENT_W * 0.28, CONTENT_W * 0.72]
    elif cols == 3:
        widths = [CONTENT_W * 0.20, CONTENT_W * 0.36, CONTENT_W * 0.44]
    elif cols == 4:
        widths = [CONTENT_W * 0.18, CONTENT_W * 0.28, CONTENT_W * 0.27, CONTENT_W * 0.27]
    else:
        widths = [CONTENT_W / cols] * cols

    prepared = []
    for row_index, row in enumerate(rows):
        padded = row + [""] * (cols - len(row))
        cells = []
        for col_index, cell in enumerate(padded):
            if row_index == 0:
                style = styles["table_header"]
            elif col_index == 0:
                style = styles["table_first"]
            else:
                style = styles["table_body"]
            cells.append(Paragraph(inline_markup(cell), style))
        prepared.append(cells)

    accent = ROLE_COLORS[role]
    light = ROLE_LIGHT[role]
    table = Table(prepared, colWidths=widths, repeatRows=1, hAlign="LEFT", splitByRow=1)
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), accent),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [WHITE, SOFT]),
                ("BACKGROUND", (0, 1), (0, -1), light),
                ("GRID", (0, 0), (-1, -1), 0.45, LINE),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 5),
                ("RIGHTPADDING", (0, 0), (-1, -1), 5),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    return table


def markdown_to_story(markdown: str, styles) -> list:
    story = []
    lines = markdown.splitlines()
    index = 0
    paragraph_lines: list[str] = []
    current_role = "공통"

    def flush_paragraph() -> None:
        nonlocal paragraph_lines
        if paragraph_lines:
            story.append(Paragraph(inline_markup(" ".join(paragraph_lines)), styles["body"]))
            paragraph_lines = []

    while index < len(lines):
        stripped = lines[index].strip()

        if stripped == "<!-- PAGEBREAK -->":
            flush_paragraph()
            story.append(PageBreak())
            index += 1
            continue

        if stripped.startswith("## "):
            flush_paragraph()
            title = stripped[3:]
            current_role = role_from_heading(title)
            story.append(section_heading(title, styles, current_role))
            index += 1
            continue

        if stripped.startswith("### "):
            flush_paragraph()
            sub_style = styles["sub"].clone(f"Sub{index}")
            sub_style.textColor = ROLE_COLORS[current_role]
            story.append(Paragraph(inline_markup(stripped[4:]), sub_style))
            index += 1
            continue

        if stripped.startswith("| "):
            flush_paragraph()
            table_lines = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                table_lines.append(lines[index].strip())
                index += 1
            rows = []
            for table_line in table_lines:
                cells = [cell.strip() for cell in table_line.strip("|").split("|")]
                if all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells):
                    continue
                rows.append(cells)
            story.append(make_table(rows, styles, current_role))
            story.append(Spacer(1, 7))
            continue

        number_match = re.match(r"^(\d+)\.\s+(.+)$", stripped)
        if number_match:
            flush_paragraph()
            story.append(
                Paragraph(
                    f"<b>{number_match.group(1)}.</b> {inline_markup(number_match.group(2))}",
                    styles["number"],
                )
            )
            index += 1
            continue

        if stripped.startswith("- "):
            flush_paragraph()
            story.append(Paragraph(inline_markup(stripped[2:]), styles["bullet"], bulletText="-"))
            index += 1
            continue

        if stripped.startswith("> "):
            flush_paragraph()
            quote_lines = []
            while index < len(lines) and lines[index].strip().startswith(">"):
                quote_lines.append(lines[index].strip()[1:].strip())
                index += 1
            quote_style = styles["quote"].clone(f"Quote{index}")
            quote_style.borderColor = ROLE_COLORS[current_role]
            quote_style.borderWidth = 1.5
            quote_style.borderPadding = 8
            quote_style.backColor = ROLE_LIGHT[current_role]
            quote_style.spaceBefore = 12
            story.append(Paragraph(inline_markup(" ".join(quote_lines)), quote_style))
            continue

        if not stripped:
            flush_paragraph()
        elif not stripped.startswith("# "):
            paragraph_lines.append(stripped)
        index += 1

    flush_paragraph()
    return story


def main() -> None:
    register_fonts()
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    source = SOURCE.read_text(encoding="utf-8")
    styles = make_styles()
    page_sources = [part.strip() for part in source.split("<!-- PAGEBREAK -->") if part.strip()]

    canvas = pdfcanvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    canvas.setTitle("Unopened Area - Revised AI Usage Technical Report")
    canvas.setAuthor("Unopened Area - Planning, Art and Development")
    canvas.setSubject("Competition submission: revised AI usage, prompts and collaboration process")

    class PageInfo:
        page = 1

    page_info = PageInfo()
    draw_cover_page(canvas, page_info)
    canvas.showPage()

    for page_index, page_source in enumerate(page_sources, start=2):
        page_info.page = page_index
        draw_body_page(canvas, page_info)
        frame = Frame(
            LEFT,
            BOTTOM + 24,
            CONTENT_W,
            PAGE_H - TOP - BOTTOM - 20,
            id=f"body_{page_index}",
            showBoundary=0,
        )
        flowables = markdown_to_story(page_source, styles)
        frame.addFromList(flowables, canvas)
        if flowables:
            raise RuntimeError(
                f"Page {page_index} content overflowed the fixed layout: {len(flowables)} flowables remain"
            )
        if page_index < len(page_sources) + 1:
            canvas.showPage()

    canvas.save()
    print(f"created={OUTPUT}")


if __name__ == "__main__":
    main()
