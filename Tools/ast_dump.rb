# frozen_string_literal: true
#
# MRI reference for the AST dump: prints a preorder walk of a file's Prism AST in
# the same format as Tools/AstDump (the C# tool), so the two can be diffed to
# verify SuperIronRuby's binary deserializer against ruby's own Prism.

require "prism"

abort "usage: ruby ast_dump.rb FILE" if ARGV.empty?

result = Prism.parse(File.binread(ARGV[0]))

def dump(node, depth)
  return if node.nil?
  name = node.class.name.split("::").last
  loc = node.location
  puts "#{"  " * depth}#{name} @#{loc.start_offset},#{loc.length}"
  node.compact_child_nodes.each { |c| dump(c, depth + 1) }
end

dump(result.value, 0)
puts "errors: #{result.errors.length}"
result.errors.each { |e| puts e.message }
