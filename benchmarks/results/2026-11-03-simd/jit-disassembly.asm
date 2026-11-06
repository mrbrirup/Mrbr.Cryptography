; Assembly listing for method Mrbr.Cryptography.Internal.Gf256Simd:GetVectorWidth(int):int (FullOpts)
; Emitting BLENDED_CODE for x64 + VEX on Windows
; FullOpts code
; optimized code
; rsp based frame
; partially interruptible
; No PGO data
; 0 inlinees with PGO data; 2 single block inlinees; 2 inlinees without PGO data

G_M000_IG01:
       push     rbx
       sub      rsp, 32
 
G_M000_IG02:
       cmp      ecx, 3
       ja       SHORT G_M000_IG08
 
G_M000_IG03:
       mov      eax, ecx
       lea      rcx, [reloc @RWD00]
       mov      ecx, dword ptr [rcx+4*rax]
       lea      rdx, G_M000_IG02
       add      rcx, rdx
       jmp      rcx
 
G_M000_IG04:
       mov      eax, 32
       jmp      SHORT G_M000_IG07
 
G_M000_IG05:
       mov      eax, 16
       jmp      SHORT G_M000_IG07
 
G_M000_IG06:
       xor      eax, eax
 
G_M000_IG07:
       add      rsp, 32
       pop      rbx
       ret      
 
G_M000_IG08:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rbx, rax
       mov      rcx, rbx
       mov      rdx, 0xD1FFAB1E
       call     [System.ArgumentOutOfRangeException:.ctor(System.String):this]
       mov      rcx, rbx
       call     CORINFO_HELP_THROW
       int3     
 
RWD00  	dd	G_M000_IG04 - G_M000_IG02
       	dd	G_M000_IG06 - G_M000_IG02
       	dd	G_M000_IG05 - G_M000_IG02
       	dd	G_M000_IG04 - G_M000_IG02

; Total bytes of code 102

; Assembly listing for method Mrbr.Cryptography.Internal.Gf256Simd:MultiplyAdd(System.ReadOnlySpan`1[byte],byte,System.Span`1[byte],int) (FullOpts)
; Emitting BLENDED_CODE for x64 + VEX on Windows
; FullOpts code
; optimized code
; rsp based frame
; fully interruptible
; No PGO data
; 0 inlinees with PGO data; 7 single block inlinees; 4 inlinees without PGO data

G_M000_IG01:
       push     r15
       push     r14
       push     r13
       push     rdi
       push     rsi
       push     rbp
       push     rbx
       sub      rsp, 64
       vxorps   xmm4, xmm4, xmm4
       vmovdqu  ymmword ptr [rsp+0x20], ymm4
       mov      esi, edx
       mov      ebx, r9d
 
G_M000_IG02:
       mov      rdi, bword ptr [r8]
       mov      ebp, dword ptr [r8+0x08]
       mov      r14, bword ptr [rcx]
       mov      r15d, dword ptr [rcx+0x08]
       xor      r13d, r13d
       test     ebx, ebx
       je       G_M000_IG07
 
G_M000_IG03:
       cmp      r15d, 16
       jl       G_M000_IG07
       movzx    rdx, sil
       lea      rcx, [rsp+0x20]
       call     [Mrbr.Cryptography.Internal.Gf256Lookup:.ctor(byte):this]
       cmp      ebx, 32
       jne      SHORT G_M000_IG05
       vmovaps  xmm0, xmmword ptr [rsp+0x20]
       vinserti128 ymm0, ymm0, xmmword ptr [rsp+0x20]
       vmovaps  xmm1, xmmword ptr [rsp+0x30]
       vinserti128 ymm1, ymm1, xmmword ptr [rsp+0x30]
       mov      eax, r15d
       sub      eax, 32
       js       SHORT G_M000_IG05
       vbroadcastss ymm2, dword ptr [reloc @RWD00]
       align    [0 bytes for IG04]
 
G_M000_IG04:
       test     r13d, r13d
       jl       G_M000_IG16
       mov      ecx, r13d
       vmovups  ymm3, ymmword ptr [r14+rcx]
       vpand    ymm4, ymm2, ymm3
       vpshufb  ymm4, ymm0, ymm4
       vpsrlw   ymm3, ymm3, 4
       vpand    ymm3, ymm3, ymm2
       vpshufb  ymm3, ymm1, ymm3
       vpxor    ymm3, ymm3, ymm4
       vpxor    ymm3, ymm3, ymmword ptr [rdi+rcx]
       vmovups  ymmword ptr [rdi+rcx], ymm3
       add      r13d, 32
       jo       G_M000_IG16
       cmp      r13d, eax
       jle      SHORT G_M000_IG04
 
G_M000_IG05:
       mov      eax, r15d
       sub      eax, 16
       cmp      r13d, eax
       jg       SHORT G_M000_IG07
       vbroadcastss xmm0, dword ptr [reloc @RWD00]
       align    [0 bytes for IG06]
 
G_M000_IG06:
       test     r13d, r13d
       jl       G_M000_IG16
       mov      ecx, r13d
       vmovups  xmm1, xmmword ptr [r14+rcx]
       vmovaps  xmm2, xmmword ptr [rsp+0x20]
       vmovaps  xmm3, xmmword ptr [rsp+0x30]
       vpand    xmm4, xmm0, xmm1
       vpshufb  xmm2, xmm2, xmm4
       vbroadcastss xmm5, dword ptr [reloc @RWD04]
       vpcmpgtb xmm4, xmm5, xmm4
       vpand    xmm2, xmm4, xmm2
       vpsrlw   xmm1, xmm1, 4
       vpand    xmm1, xmm1, xmm0
       vpshufb  xmm3, xmm3, xmm1
       vpcmpgtb xmm1, xmm5, xmm1
       vpand    xmm1, xmm1, xmm3
       vpxor    xmm1, xmm1, xmm2
       vpxor    xmm1, xmm1, xmmword ptr [rdi+rcx]
       vmovups  xmmword ptr [rdi+rcx], xmm1
       add      r13d, 16
       jo       G_M000_IG16
       cmp      r13d, eax
       jle      SHORT G_M000_IG06
 
G_M000_IG07:
       cmp      r13d, r15d
       jge      G_M000_IG12
 
G_M000_IG08:
       test     r13d, r13d
       jl       G_M000_IG13
       cmp      r15d, ebp
       jg       G_M000_IG13
       movzx    rdx, sil
 
G_M000_IG09:
       mov      eax, r13d
       lea      rcx, bword ptr [rdi+rax]
       movzx    r8, byte  ptr [rcx]
       movzx    rax, byte  ptr [r14+rax]
       mov      r10d, edx
       xor      r9d, r9d
       mov      r11d, 8
       align    [0 bytes for IG10]
 
G_M000_IG10:
       mov      ebp, r10d
       and      ebp, 1
       xor      esi, esi
       sub      esi, ebp
       and      esi, eax
       xor      r9d, esi
       lea      ebx, [rax+rax]
       sar      eax, 7
       xor      esi, esi
       sub      esi, eax
       jo       G_M000_IG16
       and      esi, 283
       mov      eax, ebx
       xor      eax, esi
       sar      r10d, 1
       dec      r11d
       jne      SHORT G_M000_IG10
 
G_M000_IG11:
       cmp      r9d, 255
       ja       G_M000_IG16
       mov      eax, r9d
       xor      r8d, eax
       cmp      r8d, 255
       ja       G_M000_IG16
       mov      r10d, r8d
       mov      byte  ptr [rcx], r10b
       add      r13d, 1
       jo       G_M000_IG16
       cmp      r13d, r15d
       jl       G_M000_IG09
 
G_M000_IG12:
       vzeroupper 
       add      rsp, 64
       pop      rbx
       pop      rbp
       pop      rsi
       pop      rdi
       pop      r13
       pop      r14
       pop      r15
       ret      
 
G_M000_IG13:
       cmp      r13d, ebp
       jae      G_M000_IG17
       mov      ecx, r13d
       add      rcx, rdi
       movzx    r8, byte  ptr [rcx]
       cmp      r13d, r15d
       jae      SHORT G_M000_IG17
       mov      edx, r13d
       movzx    rax, byte  ptr [r14+rdx]
       movzx    rdx, sil
       mov      r10d, edx
       xor      r9d, r9d
       mov      edx, 8
 
G_M000_IG14:
       mov      r11d, r10d
       and      r11d, 1
       xor      ebx, ebx
       sub      ebx, r11d
       and      ebx, eax
       xor      ebx, r9d
       mov      r9d, ebx
       lea      r11d, [rax+rax]
       sar      eax, 7
       xor      ebx, ebx
       sub      ebx, eax
       jo       SHORT G_M000_IG16
       and      ebx, 283
       mov      eax, r11d
       xor      eax, ebx
       sar      r10d, 1
       dec      edx
       jne      SHORT G_M000_IG14
 
G_M000_IG15:
       cmp      r9d, 255
       ja       SHORT G_M000_IG16
       mov      eax, r9d
       xor      eax, r8d
       cmp      eax, 255
       ja       SHORT G_M000_IG16
       mov      byte  ptr [rcx], al
       inc      r13d
       cmp      r13d, r15d
       jl       G_M000_IG13
       jmp      G_M000_IG12
 
G_M000_IG16:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
G_M000_IG17:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
RWD00  	dd	0F0F0F0Fh		; 7.05334e-30
RWD04  	dd	10101010h		; 2.84114e-29

; Total bytes of code 660

; Assembly listing for method Mrbr.Cryptography.Internal.Gf256Simd:EvaluatePolynomial(System.ReadOnlySpan`1[byte],System.ReadOnlySpan`1[byte],int,byte,System.Span`1[byte],int) (FullOpts)
; Emitting BLENDED_CODE for x64 + VEX on Windows
; FullOpts code
; optimized code
; rsp based frame
; fully interruptible
; No PGO data
; 0 inlinees with PGO data; 13 single block inlinees; 5 inlinees without PGO data

G_M000_IG01:
       push     r15
       push     r14
       push     r13
       push     r12
       push     rdi
       push     rsi
       push     rbp
       push     rbx
       sub      rsp, 72
       xor      eax, eax
       mov      qword ptr [rsp+0x28], rax
       vxorps   xmm4, xmm4, xmm4
       vmovdqa  xmmword ptr [rsp+0x30], xmm4
       mov      qword ptr [rsp+0x40], rax
       mov      ebx, r8d
       mov      rax, bword ptr [rsp+0xB0]
 
G_M000_IG02:
       mov      rsi, bword ptr [rax]
       mov      edi, dword ptr [rax+0x08]
       mov      dword ptr [rsp+0x20], edi
       mov      rbp, bword ptr [rdx]
       mov      r14d, dword ptr [rdx+0x08]
       mov      r15, bword ptr [rcx]
       mov      r13d, dword ptr [rcx+0x08]
       movzx    r12, r9b
       mov      edx, r12d
       lea      rcx, [rsp+0x28]
       call     [Mrbr.Cryptography.Internal.Gf256Lookup:.ctor(byte):this]
       mov      eax, r13d
       mov      ecx, ebx
       sub      ecx, 1
       jo       G_M000_IG21
       imul     eax, ecx
       jo       G_M000_IG21
       mov      dword ptr [rsp+0x24], eax
       xor      ecx, ecx
       cmp      dword ptr [rsp+0xB8], 32
       jne      G_M000_IG07
 
G_M000_IG03:
       vmovups  xmm0, xmmword ptr [rsp+0x28]
       vinserti128 ymm0, ymm0, xmmword ptr [rsp+0x28]
       vmovups  xmm1, xmmword ptr [rsp+0x38]
       vinserti128 ymm1, ymm1, xmmword ptr [rsp+0x38]
       mov      edx, r13d
       sub      edx, 32
       js       G_M000_IG07
       vbroadcastss ymm2, dword ptr [reloc @RWD00]
 
G_M000_IG04:
       mov      r8d, eax
       add      r8d, ecx
       jo       G_M000_IG21
       test     r8d, r8d
       jl       G_M000_IG21
       vmovups  ymm3, ymmword ptr [r8+rbp]
       mov      r8d, ebx
       sub      r8d, 2
       jo       G_M000_IG21
       test     r8d, r8d
       jl       SHORT G_M000_IG06
       align    [0 bytes for IG05]
 
G_M000_IG05:
       vpand    ymm4, ymm2, ymm3
       vpshufb  ymm4, ymm0, ymm4
       vpsrlw   ymm3, ymm3, 4
       vpand    ymm3, ymm3, ymm2
       vpshufb  ymm3, ymm1, ymm3
       vpxor    ymm3, ymm3, ymm4
       mov      r10d, r8d
       imul     r10d, r13d
       jo       G_M000_IG21
       add      r10d, ecx
       jo       G_M000_IG21
       test     r10d, r10d
       jl       G_M000_IG21
       vpxor    ymm3, ymm3, ymmword ptr [r10+rbp]
       sub      r8d, 1
       jns      SHORT G_M000_IG05
 
G_M000_IG06:
       vpand    ymm4, ymm2, ymm3
       vpshufb  ymm4, ymm0, ymm4
       vpsrlw   ymm3, ymm3, 4
       vpand    ymm3, ymm3, ymm2
       vpshufb  ymm3, ymm1, ymm3
       vpxor    ymm3, ymm3, ymm4
       test     ecx, ecx
       jl       G_M000_IG21
       mov      r8d, ecx
       vpxor    ymm3, ymm3, ymmword ptr [r15+r8]
       vmovups  ymmword ptr [rsi+r8], ymm3
       add      ecx, 32
       jo       G_M000_IG21
       cmp      ecx, edx
       jle      G_M000_IG04
 
G_M000_IG07:
       mov      edx, r13d
       sub      edx, 16
       cmp      ecx, edx
       jg       G_M000_IG19
 
G_M000_IG08:
       vbroadcastss xmm0, dword ptr [reloc @RWD00]
 
G_M000_IG09:
       mov      r8d, eax
       add      r8d, ecx
       jo       G_M000_IG21
       test     r8d, r8d
       jl       G_M000_IG21
       vmovups  xmm1, xmmword ptr [r8+rbp]
       mov      r8d, ebx
       sub      r8d, 2
       jo       G_M000_IG21
       test     r8d, r8d
       jl       SHORT G_M000_IG11
       align    [0 bytes for IG10]
 
G_M000_IG10:
       vmovups  xmm2, xmmword ptr [rsp+0x28]
       vmovups  xmm3, xmmword ptr [rsp+0x38]
       vpand    xmm4, xmm0, xmm1
       vpshufb  xmm2, xmm2, xmm4
       vbroadcastss xmm5, dword ptr [reloc @RWD04]
       vpcmpgtb xmm4, xmm5, xmm4
       vpand    xmm2, xmm4, xmm2
       vpsrlw   xmm1, xmm1, 4
       vpand    xmm1, xmm1, xmm0
       vpshufb  xmm3, xmm3, xmm1
       vpcmpgtb xmm1, xmm5, xmm1
       vpand    xmm1, xmm1, xmm3
       vpxor    xmm1, xmm1, xmm2
       mov      r10d, r8d
       imul     r10d, r13d
       jo       G_M000_IG21
       add      r10d, ecx
       jo       G_M000_IG21
       test     r10d, r10d
       jl       G_M000_IG21
       vpxor    xmm1, xmm1, xmmword ptr [r10+rbp]
       sub      r8d, 1
       jns      SHORT G_M000_IG10
 
G_M000_IG11:
       vmovups  xmm2, xmmword ptr [rsp+0x28]
       vmovups  xmm3, xmmword ptr [rsp+0x38]
       vpand    xmm4, xmm0, xmm1
       vpshufb  xmm2, xmm2, xmm4
       vbroadcastss xmm5, dword ptr [reloc @RWD04]
       vpcmpgtb xmm4, xmm5, xmm4
       vpand    xmm2, xmm4, xmm2
       vpsrlw   xmm1, xmm1, 4
       vpand    xmm1, xmm1, xmm0
       vpshufb  xmm3, xmm3, xmm1
       vpcmpgtb xmm1, xmm5, xmm1
       vpand    xmm1, xmm1, xmm3
       vpxor    xmm1, xmm1, xmm2
       test     ecx, ecx
       jl       G_M000_IG21
       mov      r8d, ecx
       vpxor    xmm1, xmm1, xmmword ptr [r15+r8]
       vmovups  xmmword ptr [rsi+r8], xmm1
       add      ecx, 16
       jo       G_M000_IG21
       cmp      ecx, edx
       jle      G_M000_IG09
       jmp      G_M000_IG19
 
G_M000_IG12:
       mov      edx, eax
       add      edx, ecx
       jo       G_M000_IG21
       cmp      edx, r14d
       jae      G_M000_IG22
       movzx    rdx, byte  ptr [rdx+rbp]
       mov      r8d, ebx
       sub      r8d, 2
       jo       G_M000_IG21
       test     r8d, r8d
       jl       G_M000_IG16
 
G_M000_IG13:
       mov      r10d, r12d
       xor      r9d, r9d
       mov      r11d, 8
       align    [0 bytes for IG14]
 
G_M000_IG14:
       mov      edi, r10d
       and      edi, 1
       xor      eax, eax
       sub      eax, edi
       and      eax, edx
       xor      r9d, eax
       lea      eax, [rdx+rdx]
       sar      edx, 7
       xor      edi, edi
       sub      edi, edx
       jo       G_M000_IG21
       and      edi, 283
       mov      edx, eax
       xor      edx, edi
       sar      r10d, 1
       dec      r11d
       jne      SHORT G_M000_IG14
 
G_M000_IG15:
       cmp      r9d, 255
       ja       G_M000_IG21
       mov      edx, r9d
       mov      r10d, r8d
       imul     r10d, r13d
       jo       G_M000_IG21
       add      r10d, ecx
       jo       G_M000_IG21
       cmp      r10d, r14d
       jae      G_M000_IG22
       movzx    r10, byte  ptr [r10+rbp]
       xor      edx, r10d
       cmp      edx, 255
       ja       G_M000_IG21
       sub      r8d, 1
       jns      G_M000_IG13
 
G_M000_IG16:
       cmp      ecx, dword ptr [rsp+0x20]
       jae      G_M000_IG22
       mov      r8d, ecx
       add      r8, rsi
       mov      r10d, r12d
       xor      r9d, r9d
       mov      r11d, 8
       align    [4 bytes for IG17]
 
G_M000_IG17:
       mov      edi, r10d
       and      edi, 1
       xor      eax, eax
       sub      eax, edi
       and      eax, edx
       xor      r9d, eax
       lea      eax, [rdx+rdx]
       sar      edx, 7
       xor      edi, edi
       sub      edi, edx
       jo       SHORT G_M000_IG21
       and      edi, 283
       mov      edx, eax
       xor      edx, edi
       sar      r10d, 1
       dec      r11d
       jne      SHORT G_M000_IG17
 
G_M000_IG18:
       cmp      r9d, 255
       ja       SHORT G_M000_IG21
       mov      edx, r9d
       mov      r10d, ecx
       movzx    r10, byte  ptr [r15+r10]
       xor      edx, r10d
       cmp      edx, 255
       ja       SHORT G_M000_IG21
       mov      byte  ptr [r8], dl
       inc      ecx
       mov      eax, dword ptr [rsp+0x24]
 
G_M000_IG19:
       cmp      ecx, r13d
       jl       G_M000_IG12
 
G_M000_IG20:
       vzeroupper 
       add      rsp, 72
       pop      rbx
       pop      rbp
       pop      rsi
       pop      rdi
       pop      r12
       pop      r13
       pop      r14
       pop      r15
       ret      
 
G_M000_IG21:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
G_M000_IG22:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
RWD00  	dd	0F0F0F0Fh		; 7.05334e-30
RWD04  	dd	10101010h		; 2.84114e-29

; Total bytes of code 990

