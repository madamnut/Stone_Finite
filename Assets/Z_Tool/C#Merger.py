import os

root_folder = r"C:\Users\PC\Desktop\YD_Unity\Stone_Finite\Assets"
cs_output_file = r"C:\Users\PC\Desktop\Tool\Cs_Files.txt"
json_output_file = r"C:\Users\PC\Desktop\Tool\Json_Files.txt"
tree_output_file = r"C:\Users\PC\Desktop\Tool\FileTree.txt"

cs_files = []
json_files = []

for dirpath, _, filenames in os.walk(root_folder):
    for fname in filenames:
        if fname.endswith(".cs"):
            cs_files.append(os.path.join(dirpath, fname))
        elif fname.lower().endswith(".json"):
            json_files.append(os.path.join(dirpath, fname))

# C# 파일 정렬
cs_filenames = [os.path.basename(f) for f in cs_files]
cs_filenames_sorted = sorted(cs_filenames)
cs_files_sorted = [f for _, f in sorted(zip(cs_filenames, cs_files))]

# JSON 파일 정렬
json_filenames = [os.path.basename(f) for f in json_files]
json_filenames_sorted = sorted(json_filenames)
json_files_sorted = [f for _, f in sorted(zip(json_filenames, json_files))]

# Cs_Files.txt 출력
with open(cs_output_file, "w", encoding="utf-8") as f:
    f.write(f"총 {len(cs_files)}개의 C# 스크립트\n")
    for filename in cs_filenames_sorted:
        f.write(f"{filename}\n")
    f.write("\n")
    for filename, file_path in zip(cs_filenames_sorted, cs_files_sorted):
        f.write(f"============[{filename}]==============\n\n")
        with open(file_path, "r", encoding="utf-8") as infile:
            f.write(infile.read())
        f.write("\n\n")

# Json_Files.txt 출력
with open(json_output_file, "w", encoding="utf-8") as f:
    f.write(f"총 {len(json_files)}개의 json\n")
    for filename in json_filenames_sorted:
        f.write(f"{filename}\n")
    f.write("\n")
    for filename, file_path in zip(json_filenames_sorted, json_files_sorted):
        f.write(f"============[{filename}]==============\n\n")
        with open(file_path, "r", encoding="utf-8") as infile:
            f.write(infile.read())
        f.write("\n\n")

# FileTree.txt 출력 (트리 구조)
def write_tree(dir_path, file, prefix=""):
    entries = sorted(os.listdir(dir_path), key=lambda x: (os.path.isfile(os.path.join(dir_path, x)), x.lower()))
    
    # .meta 파일 제외
    entries = [e for e in entries if not e.lower().endswith(".meta")]

    entries_count = len(entries)
    for idx, entry in enumerate(entries):
        full_path = os.path.join(dir_path, entry)
        connector = "└── " if idx == entries_count - 1 else "├── "
        file.write(f"{prefix}{connector}{entry}\n")
        if os.path.isdir(full_path):
            extension = "    " if idx == entries_count - 1 else "│   "
            write_tree(full_path, file, prefix + extension)

with open(tree_output_file, "w", encoding="utf-8") as f:
    f.write(f"{os.path.basename(root_folder)}/\n")
    write_tree(root_folder, f)

print("출력 완료:")
print(" -", cs_output_file)
print(" -", json_output_file)
print(" -", tree_output_file)
