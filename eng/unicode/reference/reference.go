// Command reference writes the reference of the Unicode tables: what package unicode of the Go of the oracle says about the
// categories, the scripts, the fold tables and the simple case folding (docs/design/unicode-tables.md, "How the tables are checked").
//
// It reads nothing and writes the reference to standard output; go-1.27.3.txt is that output, written by
//
//	docker run --rm -v <repository>:/repo -w /repo/eng/unicode/reference golang:1.27.3 go run reference.go
//
// The lines, in this order, with every number as six upper case hexadecimal digits and a range as lo-hi:
//
//	version <unicode.Version>
//	category <key> <range>...        for every key of unicode.Categories, by key
//	script <key> <range>...          for every key of unicode.Scripts, by key
//	foldcategory <key> <range>...    for every key of unicode.FoldCategory, by key
//	foldscript <key> <range>...      for every key of unicode.FoldScript, by key
//	alias <alias> <category>         for every key of unicode.CategoryAliases, by key
//	simplefold <code point> <next>   for every code point that unicode.SimpleFold changes, ascending
package main

import (
	"bufio"
	"fmt"
	"os"
	"sort"
	"unicode"
)

func main() {
	out := bufio.NewWriter(os.Stdout)
	defer out.Flush()

	fmt.Fprintf(out, "version %s\n", unicode.Version)
	writeTables(out, "category", unicode.Categories)
	writeTables(out, "script", unicode.Scripts)
	writeTables(out, "foldcategory", unicode.FoldCategory)
	writeTables(out, "foldscript", unicode.FoldScript)

	aliases := make([]string, 0, len(unicode.CategoryAliases))
	for alias := range unicode.CategoryAliases {
		aliases = append(aliases, alias)
	}
	sort.Strings(aliases)
	for _, alias := range aliases {
		fmt.Fprintf(out, "alias %s %s\n", alias, unicode.CategoryAliases[alias])
	}

	for c := rune(0); c <= unicode.MaxRune; c++ {
		if next := unicode.SimpleFold(c); next != c {
			fmt.Fprintf(out, "simplefold %06X %06X\n", c, next)
		}
	}
}

// writeTables writes a line for each table of a map, by key.
func writeTables(out *bufio.Writer, kind string, tables map[string]*unicode.RangeTable) {
	keys := make([]string, 0, len(tables))
	for key := range tables {
		keys = append(keys, key)
	}
	sort.Strings(keys)
	for _, key := range keys {
		fmt.Fprintf(out, "%s %s", kind, key)
		for _, r := range ranges(tables[key]) {
			fmt.Fprintf(out, " %06X-%06X", r[0], r[1])
		}
		fmt.Fprintln(out)
	}
}

// ranges lists the code points of a table as ascending ranges, merged where they touch.
func ranges(table *unicode.RangeTable) [][2]rune {
	var result [][2]rune
	add := func(lo, hi rune) {
		if n := len(result); n > 0 && result[n-1][1]+1 >= lo {
			if hi > result[n-1][1] {
				result[n-1][1] = hi
			}
			return
		}
		result = append(result, [2]rune{lo, hi})
	}
	for _, r := range table.R16 {
		for c := rune(r.Lo); c <= rune(r.Hi); c += rune(r.Stride) {
			add(c, c)
		}
	}
	for _, r := range table.R32 {
		for c := rune(r.Lo); c <= rune(r.Hi); c += rune(r.Stride) {
			add(c, c)
		}
	}
	return result
}
