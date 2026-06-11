nums = (1..10).to_a
puts nums.select(&:even?).inspect
puts nums.map { |n| n * n }.first(3).inspect
puts nums.reduce(:+)
puts nums.partition { |n| n > 5 }.inspect
puts nums.group_by { |n| n % 3 }.inspect
puts %w[apple banana cherry].sort_by(&:length).inspect
puts nums.min
puts nums.max
puts nums.sum
puts nums.tally.size
