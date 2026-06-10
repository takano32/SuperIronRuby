# Exception handling. Rescue and print a deterministic message rather than
# letting the process die (a dying snippet would be reported as a broken
# snippet, not a sir bug).
def safe_div(a, b)
  a / b
rescue ZeroDivisionError => e
  "caught: #{e.message}"
end

puts safe_div(10, 2)
puts safe_div(10, 0)

begin
  raise ArgumentError, "bad arg"
rescue => e
  puts "#{e.class}: #{e.message}"
ensure
  puts "ensure ran"
end
