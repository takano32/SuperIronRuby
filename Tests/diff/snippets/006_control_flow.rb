# if/elsif/else, while, case/when, ternary, and unless.
def classify(n)
  if n < 0
    "negative"
  elsif n.zero?
    "zero"
  else
    "positive"
  end
end

[-2, 0, 3].each { |n| puts classify(n) }

i = 0
total = 0
while i < 5
  total += i
  i += 1
end
puts total

%w[cat dog fish].each do |animal|
  desc = case animal
         when "cat" then "meow"
         when "dog" then "woof"
         else "..."
         end
  puts "#{animal}: #{desc}"
end

x = 10
puts(x > 5 ? "big" : "small")
puts "ok" unless x.negative?
