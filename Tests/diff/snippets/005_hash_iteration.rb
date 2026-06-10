# Hashes preserve insertion order (part of the contract), so iterating a hash
# literal in source order is deterministic.
h = { "one" => 1, "two" => 2, "three" => 3 }
h.each { |k, v| puts "#{k}=#{v}" }
puts h.keys.inspect
puts h.values.inspect
puts h.fetch("two")
puts h.key?("four")
puts h.merge({ "four" => 4 }).inspect
