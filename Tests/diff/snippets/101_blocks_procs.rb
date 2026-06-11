add = ->(a, b) { a + b }
puts add.call(3, 4)
puts add[10, 20]
sq = proc { |x| x * x }
puts sq.call(5)
puts [1, 2, 3].map { _1 * 10 }.inspect
counter = 0
inc = proc { counter += 1 }
inc.call; inc.call; inc.call
puts counter
