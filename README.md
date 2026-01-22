# UnmanagedString---Deobfuscator
UnmanagedString https://github.com/TheHellTower/UnmanagedString - Deobfuscator

Simple dnlib-based deobfuscator for assemblies protected with native string injection (UnmanagedString).

Features

Restores original strings from native methods

Replaces native calls back to ldstr

Removes injected native methods

Cleans leftover protection artifacts

Usage
"UnmanagedString - Deobfuscator.exe" target.exe

Output file will be saved with a modified name.

Notes

Designed specifically for the UnmanagedString injector shown in this repository

Supports x86 and x64 assemblies

PoC

