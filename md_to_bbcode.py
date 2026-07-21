#!/usr/bin/env python3
"""Convert steam-workshop.md to Steam BBCode format."""

import re
import sys

IMAGE_URLS = {
    "images/world_0.png":          "https://images.steamusercontent.com/ugc/10245062622179365909/2CB8DCE7455FFC4F010A141AB47FC776DABBD9D3/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "images/world_23.png":         "https://images.steamusercontent.com/ugc/16739171866728015075/1FAB87DACD9D5414EA52969FA6DEB98DF8DBE1C5/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "images/world_60.png":         "https://images.steamusercontent.com/ugc/10332520504072083539/B7B44786B6917501782995BBE9A96B1C2BDE3BF5/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "images/world_90.png":         "https://images.steamusercontent.com/ugc/10759519767003038994/F41A704DF523EF5253E956B57046AD3A43BA26BC/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "analysis/steam_averages.png": "https://images.steamusercontent.com/ugc/17110755943928957812/9CAE67F8C6A324B544C0C3D967EB77436FDCA65B/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "analysis/steam_seasonal.png": "https://images.steamusercontent.com/ugc/14592588807158398538/E131B55937B8CE100C177BCADAE38E7D7A331D93/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
    "images/steam_ui.png":         "https://images.steamusercontent.com/ugc/17117745568679409044/42B1731A5A15F8FF2508654E7D4AF0D7C2579339/?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true",
}

SKIP_HEADINGS = {"Copyright", "Code generation"}


def convert(text):
    lines = text.splitlines()
    out = []
    ii = 0
    in_list = False

    def flush_list():
        nonlocal in_list
        if in_list:
            out.append("[/list]")
            in_list = False

    while ii < len(lines):
        line = lines[ii]

        # Skip the document title (h1 at top of file)
        if line.startswith("# ") and ii == 0:
            ii += 1
            continue

        # Headings
        m = re.match(r"^(#{1,6}) (.+)", line)
        if m:
            heading = m.group(2).strip()

            # Skip unwanted sections entirely
            if heading in SKIP_HEADINGS:
                ii += 1
                while ii < len(lines) and not re.match(r"^#+\s", lines[ii]) and not re.match(r"^---+$", lines[ii].strip()):
                    ii += 1
                continue

            flush_list()
            # md level 2 (##) → bbcode h1, level 3+ (###) → bbcode h2
            md_level = len(m.group(1))
            bb_level = max(1, min(md_level - 1, 2))
            tag = f"h{bb_level}"
            out.append(f"[{tag}]{inline(heading)}[/{tag}]")
            ii += 1
            continue

        # Horizontal rule
        if re.match(r"^---+$", line.strip()):
            flush_list()
            out.append("[hr][/hr]")
            ii += 1
            continue

        # Blockquote → small
        m = re.match(r"^> (.+)", line)
        if m:
            flush_list()
            out.append(f"[small]{inline(m.group(1))}[/small]")
            ii += 1
            continue

        # List items
        m = re.match(r"^[-*] (.+)", line)
        if m:
            if not in_list:
                out.append("[list]")
                in_list = True
            out.append(f"[*]{inline(m.group(1))}")
            ii += 1
            continue

        flush_list()

        # Blank line
        if line.strip() == "":
            out.append("")
            ii += 1
            continue

        out.append(inline(line))
        ii += 1

    flush_list()
    result = "\n".join(out).strip()
    # Drop a leading hr from the separator after the skipped title
    if result.startswith("[hr][/hr]"):
        result = result[len("[hr][/hr]"):].lstrip("\n")
    # Collapse consecutive hrs (left by skipped sections)
    result = re.sub(r"(\[hr\]\[/hr\]\n*){2,}", "[hr][/hr]\n\n", result)
    return result


def inline(text):
    # Images before links (same syntax, images have !)
    def replace_image(m):
        src = m.group(2)
        url = IMAGE_URLS.get(src, src)
        return f"[img]{url}[/img]"
    text = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)", replace_image, text)

    # Links
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"[url=\2]\1[/url]", text)

    # Bold + italic (order matters: *** before ** before *)
    text = re.sub(r"\*\*\*(.+?)\*\*\*", r"[b][i]\1[/i][/b]", text)
    text = re.sub(r"\*\*(.+?)\*\*",     r"[b]\1[/b]",         text)
    text = re.sub(r"\*(.+?)\*",         r"[i]\1[/i]",         text)

    return text


if __name__ == "__main__":
    src = sys.argv[1] if len(sys.argv) > 1 else "steam-workshop.md"
    dst = sys.argv[2] if len(sys.argv) > 2 else "steam-workshop-bbcode.txt"

    with open(src) as ff:
        md = ff.read()

    result = convert(md)

    with open(dst, "w") as ff:
        ff.write(result + "\n")

    print(f"wrote {dst}")
