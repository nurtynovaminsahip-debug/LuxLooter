#!/usr/bin/env python3
"""
Генерирует raw-ссылки на все текстовые файлы (включая .cs и .lua).
Работает из любого места внутри Git-репозитория.
"""

import subprocess
import sys
import os
import re

try:
    import pyperclip
    HAS_PYPERCLIP = True
except ImportError:
    HAS_PYPERCLIP = False

# Расширения (добавлен .lua)
EXTENSIONS = {
    '.txt', '.md', '.py', '.sh', '.bash', '.c', '.cpp', '.h', '.hpp',
    '.java', '.js', '.ts', '.html', '.css', '.scss', '.json', '.xml',
    '.yaml', '.yml', '.conf', '.ini', '.cfg', '.log', '.rst', '.tex',
    '.bib', '.go', '.rs', '.swift', '.kt', '.sql', '.r', '.php', '.rb',
    '.pl', '.pm', '.t', '.pod', '.podspec', '.makefile', '.mk', '.cmake',
    '.d', '.nim', '.v', '.zig', '.fs', '.fst', '.fsx', '.clj', '.cljs',
    '.edn', '.lisp', '.el', '.scm', '.ss', '.rkt', '.hs', '.lhs', '.agda',
    '.idr', '.coffee', '.litcoffee', '.jade', '.pug', '.slim', '.haml',
    '.ejs', '.mustache', '.handlebars', '.hbs', '.vue', '.svelte',
    '.tsx', '.jsx', '.mjs', '.cjs', '.mts', '.cts',
    '.cs',   # C#
    '.lua'   # Lua – добавлено!
}

def get_repo_root():
    try:
        root = subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], stderr=subprocess.DEVNULL, text=True).strip()
        return root
    except:
        print("❌ Ошибка: не удалось найти корень Git-репозитория.", file=sys.stderr)
        sys.exit(1)

def get_git_info():
    try:
        remote_url = subprocess.check_output(['git', 'config', '--get', 'remote.origin.url'], stderr=subprocess.DEVNULL, text=True).strip()
        branch = subprocess.check_output(['git', 'rev-parse', '--abbrev-ref', 'HEAD'], stderr=subprocess.DEVNULL, text=True).strip()
        return remote_url, branch
    except:
        print("❌ Ошибка при получении данных Git.", file=sys.stderr)
        sys.exit(1)

def convert_to_raw_base(url):
    url = re.sub(r'\.git$', '', url.strip())
    m = re.match(r'git@github\.com:(.+)/(.+)', url) or re.match(r'https://github\.com/(.+)/(.+)', url)
    if m:
        return f"https://raw.githubusercontent.com/{m.group(1)}/{m.group(2)}"
    m = re.match(r'git@gitlab\.com:(.+)/(.+)', url) or re.match(r'https://gitlab\.com/(.+)/(.+)', url)
    if m:
        return f"https://gitlab.com/{m.group(1)}/{m.group(2)}/-/raw"
    m = re.match(r'git@bitbucket\.org:(.+)/(.+)', url) or re.match(r'https://bitbucket\.org/(.+)/(.+)', url)
    if m:
        return f"https://bitbucket.org/{m.group(1)}/{m.group(2)}/raw"
    return None

def get_text_files():
    try:
        all_files = subprocess.check_output(['git', 'ls-files'], stderr=subprocess.DEVNULL, text=True).splitlines()
        return [f for f in all_files if os.path.splitext(f)[1].lower() in EXTENSIONS]
    except:
        return []

def copy_to_clipboard(text):
    if HAS_PYPERCLIP:
        try:
            pyperclip.copy(text)
            print("✅ Скопировано в буфер (pyperclip)")
            return
        except:
            pass
    if sys.platform == 'darwin':
        subprocess.Popen(['pbcopy'], stdin=subprocess.PIPE).communicate(text.encode())
        print("✅ Скопировано (pbcopy)")
    elif sys.platform.startswith('linux'):
        if subprocess.call(['which', 'xclip'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0:
            subprocess.Popen(['xclip', '-selection', 'clipboard'], stdin=subprocess.PIPE).communicate(text.encode())
            print("✅ Скопировано (xclip)")
        elif subprocess.call(['which', 'xsel'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0:
            subprocess.Popen(['xsel', '--clipboard', '--input'], stdin=subprocess.PIPE).communicate(text.encode())
            print("✅ Скопировано (xsel)")
        else:
            print("⚠️ Утилита копирования не найдена.")
    elif sys.platform == 'win32':
        try:
            subprocess.Popen(['clip'], stdin=subprocess.PIPE, shell=True).communicate(text.encode())
            print("✅ Скопировано (clip)")
        except:
            print("⚠️ Не удалось скопировать.")

def main():
    repo_root = get_repo_root()
    os.chdir(repo_root)
    remote_url, branch = get_git_info()
    raw_base = convert_to_raw_base(remote_url)
    if not raw_base:
        print("❌ Неподдерживаемый хостинг (нужен GitHub, GitLab или Bitbucket).", file=sys.stderr)
        sys.exit(1)
    files = get_text_files()
    if not files:
        print("ℹ️ Текстовые файлы не найдены.")
        sys.exit(0)
    links = [f"{raw_base}/{branch}/{f}" for f in files]
    output = "\n".join(links)
    print(output)
    copy_to_clipboard(output)

if __name__ == "__main__":
    main()
