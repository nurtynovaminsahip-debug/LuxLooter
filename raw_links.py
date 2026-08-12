#!/usr/bin/env python3
"""
Выводит только имена текстовых файлов (включая .cs) без путей.
Не генерирует ссылки.
"""

import subprocess
import sys
import os

try:
    import pyperclip
    HAS_PYPERCLIP = True
except ImportError:
    HAS_PYPERCLIP = False

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
    '.cs'
}

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
        except: pass
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
    files = get_text_files()
    if not files:
        print("ℹ️ Текстовые файлы не найдены.")
        sys.exit(0)
    # Берём только имена файлов без путей
    names = [os.path.basename(f) for f in files]
    output = "\n".join(names)
    print(output)
    copy_to_clipboard(output)

if __name__ == "__main__":
    main()