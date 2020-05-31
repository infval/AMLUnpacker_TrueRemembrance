meta:
  id: aml_arciver
  file-extension: arc
  endian: le
  encoding: ascii
seq:
  - id: hdr
    type: header
  - id: files
    type: file
    repeat: expr
    repeat-expr: hdr.file_count
types:
  header:
    seq:
      - id: magic
        contents: 'AML_Arciver'
      - id: padding
        size: 128 - 11
      - id: file_count
        type: u4
  file:
    seq:
      - id: path
        type: strz
        size: 64
      - id: size
        type: u4
      - id: data
        size: size
