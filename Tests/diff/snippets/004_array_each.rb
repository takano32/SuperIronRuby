# Array iteration, map, select, and inspect of the results.
nums = [1, 2, 3, 4, 5]
nums.each { |n| print n, " " }
puts
puts nums.map { |n| n * n }.inspect
puts nums.select { |n| n.even? }.inspect
puts nums.reduce(0) { |acc, n| acc + n }
puts nums.reverse.inspect
puts [3, 1, 2].sort.inspect
