# User-defined class with a custom to_s so output is deterministic
# (the default #inspect would embed an object address).
class Point
  attr_reader :x, :y

  def initialize(x, y)
    @x = x
    @y = y
  end

  def +(other)
    Point.new(@x + other.x, @y + other.y)
  end

  def to_s
    "(#{@x}, #{@y})"
  end
end

a = Point.new(1, 2)
b = Point.new(3, 4)
puts a
puts b
puts(a + b)
puts a.x
puts b.y
