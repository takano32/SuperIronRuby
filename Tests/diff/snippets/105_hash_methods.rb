h = { a: 1, b: 2, c: 3 }
puts h.keys.inspect
puts h.values.inspect
puts h.map { |k, v| [k, v * 10] }.to_h.inspect
puts h.select { |k, v| v > 1 }.inspect
puts h.fetch(:b)
puts h.merge(d: 4).size
total = 0
h.each_value { |v| total += v }
puts total
