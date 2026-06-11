def describe(x)
  case x
  in Integer => n if n > 10
    "big int #{n}"
  in Integer
    "int"
  in [a, b]
    "pair #{a},#{b}"
  in {name:}
    "named #{name}"
  else
    "other"
  end
end
puts describe(5)
puts describe(50)
puts describe([1, 2])
puts describe({name: "x"})
puts describe("str")
